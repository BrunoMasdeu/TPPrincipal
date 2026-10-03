# Arquitectura de red

Esta carpeta contiene la estructura inicial para separar lobby, aparición de jugadores, ciclo común de partida y reglas específicas de TDM y CTF.

## Estado actual

La arquitectura nueva está preparada, pero todavía no está integrada al juego.

El comportamiento actual continúa dependiendo de los scripts existentes, entre ellos:

- `LobbyPlayerSpawner`
- `PlayerNetworkSetup`
- `GameManager`
- `ConnectionManager`
- `MenuManager`
- `RaceStatusUI`

Los archivos existentes no deben moverse, renombrarse ni reemplazarse como parte de esta preparación.

## Responsabilidades futuras

- `Core`: identificadores y estados compartidos.
- `Configuration`: configuración editable de cada modo.
- `Lobby`: participantes y validaciones anteriores a la partida.
- `Spawning`: puntos de aparición y spawn autoritativo.
- `Match`: fases, tiempo, resultado y estadísticas comunes.
- `Match/Modes/TDM`: reglas y estado exclusivo de Team Deathmatch.
- `Match/Modes/CTF`: reglas, banderas y estado exclusivo de Captura la Bandera.

## Límites de esta preparación

- Los managers nuevos no están agregados a escenas ni prefabs.
- No existen puntajes, capturas ni reglas funcionales de TDM o CTF.
- No se agregaron RPC, `NetworkVariable` ni `NetworkList` para los modos nuevos.
- Las armas, la salud, la muerte, el respawn y la interfaz todavía no están integrados con esta estructura.
- `NetworkMatchManager` no reemplaza todavía al `GameManager` actual.

Cada integración deberá realizarse en una tarea posterior y conservar la autoridad del servidor.
