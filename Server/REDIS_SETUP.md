# Redis 환경 세팅 가이드

> 이 파일은 새 데스크탑에서 Claude Code로 열었을 때 Redis 환경을 현재 개발 환경과 동일하게 맞추기 위한 가이드입니다.
> Claude Code에게 이 파일을 보여주며 "여기 적힌 대로 세팅해줘" 라고 하면 자동으로 진행합니다.

---

## 현재 적용된 내용 (코드베이스에 이미 반영됨)

아래 항목은 이미 소스코드에 반영되어 있으므로 git pull 이후 별도 코드 수정 불필요:

- `Server/Server/Server.csproj` — `StackExchange.Redis 2.8.16` 패키지 추가됨
- `Server/Server/Session/AccountManager.cs` — HashSet → Redis SET으로 교체됨
- `Server/docker-compose.yml` — Redis 7 컨테이너 정의 파일 존재

---

## Claude Code가 해야 할 작업

### 전제 조건 확인 (사람이 직접)

- [ ] Docker Desktop 설치 완료 (`docker --version` 터미널에서 확인)
- [ ] Docker Desktop 실행 중

### Step 1 — Redis 컨테이너 실행

`docker-compose.yml`이 있는 폴더에서 아래 명령어 실행:

```bash
cd /c/Users/user/Desktop/Development/Unity/Echodia/Server
docker compose up -d
```

실행 후 확인:

```bash
docker ps
```

`server-redis-1` 컨테이너가 `Up` 상태면 완료.

### Step 2 — NuGet 패키지 복원

```bash
cd /c/Users/user/Desktop/Development/Unity/Echodia/Server/Server
dotnet restore
```

### Step 3 — 빌드 확인

```bash
dotnet build
```

`Build succeeded` 가 나오면 세팅 완료.

### Step 4 — 이 파일 삭제

모든 스텝이 완료되면 이 파일은 더 이상 필요 없습니다. 삭제해주세요:

```bash
rm /c/Users/user/Desktop/Development/Unity/Echodia/Server/REDIS_SETUP.md
```

---

## 검증 방법

1. 서버 실행
2. 클라이언트로 로그인
3. RedisInsight(`localhost:6379` 연결) 또는 아래 명령어로 로그인 상태 확인:

```bash
docker exec -it server-redis-1 redis-cli
SMEMBERS loggedIn
```

로그인한 AccountId가 출력되면 정상 동작.

---

## Redis 관련 명령어 치트시트

| 상황 | 명령어 | 실행 위치 |
|------|--------|-----------|
| Redis 시작 | `docker compose up -d` | `Server/` 폴더 |
| Redis 상태 확인 | `docker ps` | 어디서나 |
| Redis 종료 | `docker compose down` | `Server/` 폴더 |
| Redis CLI 접속 | `docker exec -it server-redis-1 redis-cli` | 어디서나 |
| 로그인 목록 확인 | `SMEMBERS loggedIn` | redis-cli 내부 |

> Docker Desktop이 자동 시작으로 설정되어 있으면 PC를 켤 때 Redis도 자동으로 실행됩니다.
