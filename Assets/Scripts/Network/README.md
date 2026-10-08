# Arquitectura de red (COG-929, Fase 1)

## Flujo implementado

1. `MenuManager` elige un `GameModeDefinition` y una cantidad válida. TDM y CTF usan Relay; la carrera individual usa host local. El menú no decide el nombre de la escena.
2. `ConnectionManager` conserva Unity Services, autenticación, Relay, mensajes y botones de conexión. Antes de iniciar el host entrega la configuración a `NetworkSessionBootstrap`.
3. `NetworkSessionBootstrap` crea y spawnea una sola raíz persistente `NetworkSessionRoot`. La raíz contiene `NetworkLobbySession`, `NetworkPlayerSpawner` y `NetworkMatchManager`.
4. `NetworkLobbySession` es la fuente de jugadores, equipos, ready, modo, cantidad y fase Lobby/Loading/InMatch. Las peticiones de clientes se validan en el servidor. Sólo la sesión ordena cargar `GameModeDefinition.SceneName`.
5. Al completar la carga, `NetworkPlayerSpawner` comprueba clientes y timeouts, lee el equipo desde la sesión, busca `TeamSpawnPoint`, crea un `PlayerObject` por cliente con el ownership correcto y confirma el spawn a la sesión.
6. Cuando la sesión entra en `InMatch`, `NetworkMatchManager` recorre WaitingForPlayers → Countdown → Playing → Finished. Replica fase, duración, timestamps y resultado, no un cronómetro por frame. Cada cliente calcula `TimeRemaining` con el reloj del servidor.

## Configuración de escenas y prefabs

- `NetworkSessionRoot.prefab` está registrado en `DefaultNetworkPrefabs` y referenciado por `NetworkSessionBootstrap` en `MenuScene`.
- `TdmGameModeDefinition` y `CtfGameModeDefinition` usan temporalmente `tdm_scene_prueba`. Admiten 2, 4, 6 u 8 jugadores; el menú arranca en 2 para poder probar Editor + build. El mapa contiene cuatro `TeamSpawnPoint` rojos y cuatro azules; CTF todavía no tiene escenario ni reglas propias.
- `RaceGameModeDefinition` usa `CentroEntrenamiento`, permite un jugador y no requiere equipos. El objeto `Respawn` de esa escena tiene un `TeamSpawnPoint` con `TeamId.None`.
- Los assets de modo controlan escena, duración, límite de puntuación, cantidades permitidas y si se necesitan equipos. Las reglas de marcador TDM/CTF siguen pendientes.

## Puentes temporales

- `LobbyPlayerSpawner` permanece en `MenuScene` para no romper referencias antiguas. Conserva aprobación de conexión; para sesiones configuradas toma la capacidad desde `NetworkSessionBootstrap`/`NetworkLobbySession`. Sus callbacks de spawn y comienzo de carrera sólo actúan cuando no hay una sesión de modo nuevo. La carrera individual configurada usa el spawner nuevo. `RaceStartReadiness` queda como wrapper de compatibilidad, pero el consumidor usa `PlayerSpawnReadinessRules`.
- `GameManager` conserva sus `NetworkVariable` y API pública para `RaceStatusUI`, `Llegada` y tests antiguos. Se suscribe al manager común para iniciar la carrera configurada y traducir llegada/timeout a `MatchResultData`. La dirección de adaptación es carrera → ciclo común; no hay un bucle de escritura de vuelta. Su cronómetro legado aún se replica por frame, sólo para compatibilidad de la carrera.
- Los controles `OnGUI` de selección de modo/cantidad y equipo/ready son provisionales para probar el flujo. La UI definitiva puede llamar a `SelectTdm`, `SelectCtf`, `SelectPlayerCount`, `SelectRedTeam`, `SelectBlueTeam` y `ToggleReady`.

No retire `LobbyPlayerSpawner`, `RaceStartReadiness` ni las variables antiguas de `GameManager` hasta migrar escenas, consumidores y tests de carrera y comprobar su resultado en Unity.

## Límites deliberados

- TDM y CTF todavía no calculan kills, capturas, respawns, marcador ni ganador por puntuación. Sin un manager específico que cierre la partida, el manager común cierra por tiempo como empate.
- Armas, salud, daño, muertes y UI específica de cada modo no están conectados a esta fase.
- La comprobación real de Unity, Test Runner, host/cliente por Relay y carrera individual sigue pendiente de la validación manual del usuario. La compilación C# no sustituye esas pruebas.
