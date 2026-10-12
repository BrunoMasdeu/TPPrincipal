# Arquitectura de red (COG-929, Fase 1)

## Flujo implementado

1. `MenuManager` elige un `GameModeDefinition` y una cantidad válida. TDM y CTF usan Relay; la carrera individual usa host local. El menú no decide el nombre de la escena.
2. `ConnectionManager` conserva Unity Services, autenticación, Relay, mensajes y botones de conexión. Antes de iniciar el host entrega la configuración a `NetworkSessionBootstrap`.
3. `NetworkSessionBootstrap` crea y spawnea una sola raíz persistente `NetworkSessionRoot`. La raíz contiene `NetworkLobbySession`, `NetworkPlayerSpawner` y `NetworkMatchManager`.
4. `NetworkLobbySession` es la fuente de jugadores, equipos, ready, modo, mapa y fase Lobby/Loading/InMatch. Las peticiones de clientes se validan en el servidor. Sólo el host puede cambiar el mapa y la sesión ordena cargar la escena seleccionada.
5. Al completar la carga, `NetworkPlayerSpawner` comprueba clientes y timeouts, lee el equipo desde la sesión, busca `TeamSpawnPoint`, crea un `PlayerObject` por cliente con el ownership correcto y confirma el spawn a la sesión.
6. Cuando la sesión entra en `InMatch`, `NetworkMatchManager` recorre WaitingForPlayers → Countdown → Playing → Finished. Replica fase, duración, timestamps y resultado, no un cronómetro por frame. Cada cliente calcula `TimeRemaining` con el reloj del servidor.

## Configuración de escenas y prefabs

- `NetworkSessionRoot.prefab` está registrado en `DefaultNetworkPrefabs` y referenciado por `NetworkSessionBootstrap` en `MenuScene`.
- El catálogo contiene `TdmTestMapDefinition`, `CtfTestMapDefinition` y `ArenaIndustrialMapDefinition`. La sala filtra las opciones por modo: TDM ofrece su mapa de prueba y Arena Industrial; CTF ofrece su mapa de prueba y Arena Industrial.
- `NetworkSessionRoot.prefab` referencia el catálogo. La selección se replica, sólo puede cambiarla el host durante el lobby, reinicia los estados ready al modificarse y se conserva al regresar desde una partida.
- `ArenaIndustrial` contiene cuatro `TeamSpawnPoint` rojos, cuatro azules, una base y un `FlagSpawnPoint` por equipo. No contiene cámaras, jugadores, `NetworkManager` ni sistemas de sesión duplicados.
- `tdm_scene_prueba` y `ctf_scene_prueba` se conservan como escenarios de prueba seleccionables.
- `RaceGameModeDefinition` usa `CentroEntrenamiento`, permite un jugador y no requiere equipos. El objeto `Respawn` de esa escena tiene un `TeamSpawnPoint` con `TeamId.None`.
- Los assets de modo controlan duración, límite de puntuación, cantidades permitidas y si se necesitan equipos. Los assets de mapa controlan el nombre de escena y los modos compatibles. El nombre de escena del modo queda como respaldo para configuraciones antiguas sin catálogo.

## Puentes temporales

- `LobbyPlayerSpawner` permanece en `MenuScene` para no romper referencias antiguas. Conserva aprobación de conexión; para sesiones configuradas toma la capacidad desde `NetworkSessionBootstrap`/`NetworkLobbySession`. Sus callbacks de spawn y comienzo de carrera sólo actúan cuando no hay una sesión de modo nuevo. La carrera individual configurada usa el spawner nuevo. `RaceStartReadiness` queda como wrapper de compatibilidad, pero el consumidor usa `PlayerSpawnReadinessRules`.
- `GameManager` conserva sus `NetworkVariable` y API pública para `RaceStatusUI`, `Llegada` y tests antiguos. Se suscribe al manager común para iniciar la carrera configurada y traducir llegada/timeout a `MatchResultData`. La dirección de adaptación es carrera → ciclo común; no hay un bucle de escritura de vuelta. Su cronómetro legado aún se replica por frame, sólo para compatibilidad de la carrera.
- Los controles `OnGUI` de selección de modo/cantidad, mapa, equipo y ready son provisionales para probar el flujo. La UI definitiva puede llamar a `SelectTdm`, `SelectCtf`, `SelectPlayerCount`, `SelectMap`, `SelectRedTeam`, `SelectBlueTeam` y `ToggleReady`.

No retire `LobbyPlayerSpawner`, `RaceStartReadiness` ni las variables antiguas de `GameManager` hasta migrar escenas, consumidores y tests de carrera y comprobar su resultado en Unity.

## Estado de validación y límites deliberados

- TDM y CTF funcionan en Editor + build con dos jugadores mediante Relay. Se comprobaron equipos, spawns, ownership, movimiento, gancho, armas, daño, muerte, marcador y respawn.
- En Arena Industrial se comprobó el ciclo CTF: recoger, capturar, soltar al morir, recoger una bandera caída, devolución aliada y retorno automático.
- El selector se comprobó en host y cliente, incluido el filtrado por modo, la restricción de escritura al host y la persistencia de Arena Industrial al volver al lobby.
- La UI `OnGUI`, los cuadros de debug y las banderas lógicas sin modelo 3D siguen siendo provisionales.
- Las cantidades de 4, 6 y 8 jugadores todavía requieren una prueba multicliente independiente.
