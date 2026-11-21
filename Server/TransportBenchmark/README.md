# 게임 세션의 TLS 비용 측정

EC2의 운영 포트를 변경하지 않고 루프백에서 실행한다. 같은 프로세스에서 이전 소켓 세션과 현재 게임 TLS 세션이 동일한 길이와 일련번호를 가진 프레임을 돌려준다. `StreamTcp`와 `StreamTls`는 같은 읽기·쓰기 루프에서 스트림만 바꾼 대조군이다. 생산 서버에는 평문 모드를 제공하지 않는다.

```powershell
dotnet run -c Release --project Server/TransportBenchmark/TransportBenchmark.csproj | Out-File -Encoding utf8 run.jsonl
python Server/TransportBenchmark/summarize.py run.jsonl
```

Windows에서 사용자 키 저장소가 없는 SSH 세션은 실행 인수 `-- --machine-key`를 추가한다. 시험용 localhost 인증서는 프로세스가 생성하고 클라이언트가 해당 인증서와 호스트명을 확인한다. TLS 1.2로 고정하고 세션 재개를 끈다.

- 64바이트와 1,024바이트를 연결 1개와 8개에서 각각 측정한다. 각 연결은 한 번에 하나의 응답을 기다린다.
- 예열 후 연결당 600회 측정하며, 경로 순서를 정방향과 역방향으로 번갈아 여섯 번 실행한다. 모든 응답 바이트를 검증한다.
- 표는 각 회차의 왕복 지연 백분위수와 처리량을 구한 뒤 회차 간 중앙값을 사용한다. 할당량은 측정 구간에서 클라이언트와 서버를 합친 프로세스 전체의 관리 힙 할당량이다. 최초 TCP 연결 및 TLS 핸드셰이크는 별도로 기록한다.
- 합성 프레임에는 계정 데이터가 없다. DB 조회, 게임 룸 연산, 인터넷 구간, Unity 프레임 스케줄링은 이 측정에서 제외한다. 실행 환경과 빌드가 다른 파일은 별도로 집계한다.
