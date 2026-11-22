#!/bin/bash
set -euo pipefail
umask 077
conf=/etc/echodia-redis
handoff=/mnt/c/ProgramData/EchodiaRedis
data=/var/lib/echodia-redis
mode=$1
host=$2

die() { echo "$*" >&2; exit 1; }
[ "$(id -u)" = 0 ] || die 'Run as root inside the dedicated Redis distro.'
[[ "$host" =~ ^[0-9]+(\.[0-9]+){3}$ ]] || die 'Expected IPv4 address.'
guest_ip() {
    ip -4 route get "$host" | awk 'NR==1 {for (i=1; i<NF; i++) if ($i=="src") {print $(i+1); exit}}'
}

if [ "$mode" = ip ]; then guest_ip; exit; fi
if [ "$mode" = health ]; then
    # A successful response requires the live listener, the matching IP SAN and anonymous rejection.
    reply=$(printf '*1\r\n$4\r\nPING\r\n*1\r\n$4\r\nQUIT\r\n' |
        timeout 4 openssl s_client -quiet -ign_eof -connect 127.0.0.1:6380 -verify_return_error \
            -verify_ip "$host" -CAfile "$conf/ca.pem" 2>/dev/null || true)
    [[ "$reply" == '-NOAUTH Authentication required.'* ]] || exit 1
    [ -z "$(ss -H -ltn 'sport = :6379')" ] || die 'Unexpected plaintext Redis listener.'
    exit 0
fi
[[ "$mode" = prepare || "$mode" = serve ]] || die 'Unknown mode.'
verifier=$3
[[ "$verifier" =~ ^[0-9a-f]{64}$ ]] || die 'Invalid account verifier.'
[ -d "$handoff" ] || die 'Missing Windows certificate handoff directory.'

install_vendor_redis() (
    source /etc/os-release
    [ "$ID:$VERSION_CODENAME" = ubuntu:noble ] || die 'Expected Ubuntu 24.04 LTS.'
    export DEBIAN_FRONTEND=noninteractive
    # This distro runs only our foreground Redis. Keep the package-managed listener disabled even across upgrades.
    install -d /etc/systemd/system
    for unit in redis.service redis-server.service; do
        if [ -L "/etc/systemd/system/$unit" ]; then
            [ "$(readlink "/etc/systemd/system/$unit")" = /dev/null ] || die "Unexpected existing $unit"
        else
            ln -s /dev/null "/etc/systemd/system/$unit"
        fi
    done
    apt-get -o Acquire::Retries=3 update -qq
    apt-get -o Acquire::Retries=3 install --no-install-recommends -y ca-certificates curl gpg openssl iproute2
    tmp=$(mktemp -d)
    trap 'rm -rf -- "$tmp"' EXIT
    chmod 700 "$tmp"
    export GNUPGHOME="$tmp"
    curl --fail --silent --show-error --location --proto '=https' --retry 3 -o "$tmp/key.asc" https://packages.redis.io/gpg
    fingerprints=$(gpg --batch --show-keys --with-colons "$tmp/key.asc" | awk -F: '$1=="pub"{want=1} $1=="fpr" && want {print $10;want=0}')
    [ "$fingerprints" = 54318FA4052D1E61A6B6F7BB5F4349D6BF53AA0C ] || die 'Unexpected Redis archive signing key.'
    gpg --batch --dearmor --output "$tmp/redis.gpg" "$tmp/key.asc"
    install -d -m 755 /etc/apt/keyrings
    install -m 644 "$tmp/redis.gpg" /etc/apt/keyrings/echodia-redis.gpg
    printf '%s\n' 'deb [arch=amd64 signed-by=/etc/apt/keyrings/echodia-redis.gpg] https://packages.redis.io/deb noble main' \
        > /etc/apt/sources.list.d/echodia-redis.list
    printf '%s\n' 'Package: redis redis-server redis-tools' 'Pin: origin packages.redis.io' 'Pin-Priority: 1001' \
        > /etc/apt/preferences.d/echodia-redis
    chmod 644 /etc/apt/sources.list.d/echodia-redis.list /etc/apt/preferences.d/echodia-redis
    apt-get -o Acquire::Retries=3 update -qq
    candidate=$(apt-cache policy redis-server | awk '$1=="Candidate:" {print $2}')
    # Check the candidate is served by the Redis archive even when the Ubuntu archive is present.
    apt-cache madison redis-server | awk -F'|' -v version="$candidate" '{gsub(/^[ \t]+|[ \t]+$/,"",$2); if ($2==version && $3 ~ /https:\/\/packages\.redis\.io\/deb /) found=1} END{exit !found}'
    # Debian packages invoke policy-rc.d before starting services via SysV as well as systemd.
    policy=/usr/sbin/policy-rc.d
    if [ -e "$policy" ] || [ -L "$policy" ]; then cp -a "$policy" "$tmp/policy.original"; fi
    restore_policy() {
        rm -f -- "$policy"
        if [ -e "$tmp/policy.original" ] || [ -L "$tmp/policy.original" ]; then cp -a "$tmp/policy.original" "$policy"; fi
        rm -rf -- "$tmp"
    }
    trap restore_policy EXIT
    printf '#!/bin/sh\nexit 101\n' > "$tmp/deny-start"
    rm -f -- "$policy"
    install -m 755 "$tmp/deny-start" "$policy"
    apt-get -o Acquire::Retries=3 install --no-install-recommends -y "redis-server=$candidate" "redis-tools=$candidate"
    dpkg-query -W -f='Version: ${Version}\n' redis-server > "$conf/package-version"
)

if [ "$mode" = prepare ]; then
    install -d -m 750 -o root "$conf"
    if [ ! -s "$conf/package-version" ] || ! command -v redis-server >/dev/null; then install_vendor_redis; fi
    chgrp redis "$conf"
    install -d -m 750 -o redis -g redis "$data"
    if [ ! -f "$conf/server.key" ]; then
        openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$conf/server.key.pending"
        mv "$conf/server.key.pending" "$conf/server.key"
    fi
    chown root:redis "$conf/server.key"; chmod 640 "$conf/server.key"
    openssl req -new -key "$conf/server.key" -subj "/CN=$host" -addext "subjectAltName=IP:$host" \
        -addext "extendedKeyUsage=serverAuth" -out "$conf/server.csr"
    install -m 600 "$conf/server.csr" "$handoff/state/server.csr"
    exit 0
fi

# Verify the leaf, its private SAN and that this machine owns the corresponding private key.
openssl verify -purpose sslserver -verify_ip "$host" -CAfile "$handoff/ca.pem" "$handoff/server.pem"
leaf=$(openssl x509 -pubkey -noout -in "$handoff/server.pem" | openssl pkey -pubin -outform DER | sha256sum)
key=$(openssl pkey -in "$conf/server.key" -pubout -outform DER | sha256sum)
[ "$leaf" = "$key" ] || die 'Returned certificate does not match the Redis host key.'
for pem in ca.pem server.pem; do install -m 640 -o root -g redis "$handoff/$pem" "$conf/$pem"; done
printf 'user default off\nuser echodia reset on #%s ~loggedIn -@all +ping +echo +hello +info +client|setname +client|setinfo +client|id +select +sadd +srem +sismember\n' "$verifier" > "$conf/users.acl.new"
chown root:redis "$conf/users.acl.new"; chmod 640 "$conf/users.acl.new"; mv -f "$conf/users.acl.new" "$conf/users.acl"
guest=$(guest_ip)
[[ "$guest" =~ ^[0-9]+(\.[0-9]+){3}$ ]] || die 'Missing WSL NAT guest address.'
cat > "$conf/redis.conf" <<EOF
bind 127.0.0.1 $guest
protected-mode yes
port 0
tls-port 6380
tls-cert-file $conf/server.pem
tls-key-file $conf/server.key
tls-ca-cert-file $conf/ca.pem
tls-auth-clients no
tls-protocols "TLSv1.2 TLSv1.3"
aclfile $conf/users.acl
databases 1
dir $data
appendonly yes
appendfsync always
save ""
maxmemory-policy noeviction
daemonize no
logfile ""
EOF
chown root:redis "$conf/redis.conf"; chmod 640 "$conf/redis.conf"
exec runuser -u redis -- /usr/bin/redis-server "$conf/redis.conf"
