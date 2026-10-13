# Datos y eventos para el HUD (Fase 6)

Esta guía es para conectar una interfaz visual sin modificar las reglas de la partida. Los managers publican datos y avisos; el script del HUD escucha el aviso, lee los datos actuales y actualiza sus propios controles. Un aviso no aplica daño ni cambia el marcador.

| Vista | Leer de | Aviso de cambio |
| --- | --- | --- |
| Marcador Rojo/Azul y límite | `TdmMatchManager.RedEliminations`, `BlueEliminations`, `TryGetOfficialScoreLimit` | `ScoreChanged` |
| Tabla de jugadores: ID, equipo, kills, deaths | `TdmMatchManager.PlayerStatsCount` y `TryGetPlayerStats` | `PlayerStatsChanged` |
| Modo y cantidad elegida | `NetworkLobbySession.SelectedGameModeId`, `SelectedPlayerCount` | `LobbyStateChanged` |
| Fase y resultado | `NetworkMatchManager.Phase`, `Result` | `PhaseChanged`, `MatchFinished` |
| Tiempo y cuenta regresiva | `NetworkMatchManager.TimeRemaining`, `CountdownRemaining` | `PhaseChanged`; refrescar sólo el texto local periódicamente. `CountdownSecondChanged` sirve para una señal una vez por segundo. |
| Vida y estado del jugador local | `NetworkPlayerHealth.CurrentHealth`, `MaxHealth`, `LifeState` | `HealthChanged`, `LifeStateChanged` |
| Tiempo para reaparecer | `NetworkPlayerHealth.RespawnRemaining` | `LifeStateChanged`; refrescar sólo el texto local periódicamente. |
| Balas y recarga del jugador local | `NetworkPlayerCombat.Ammunition`, `MagazineSize`, `ReloadRemaining` | `AmmunitionChanged`, `ReloadChanged` |
| Dinero del jugador local | `NetworkPlayerCombat.Money` | `MoneyChanged` (sólo propietario) |
| Aviso temporal de muerte | `PlayerDeathInfo` recibido por `NetworkMatchManager.DeathAnnounced` | `DeathAnnounced` |

El ID identifica al jugador mientras no exista un nombre replicado. El orden de `PlayerStatsCount` no es una identidad permanente: para una fila estable, usar `PlayerMatchStats.ClientId`. `TdmMatchManager` sólo ofrece datos TDM; su componente permanece inactivo para otros modos.

Ejemplo de conexión para el HUD de TDM:

```csharp
// Al aparecer la interfaz, después de encontrar la sesión activa:
tdm.ScoreChanged += RefreshScore;
tdm.PlayerStatsChanged += RefreshPlayerTable;
match.PhaseChanged += OnPhaseChanged;
match.MatchFinished += OnMatchFinished;

// Leer inmediatamente: un evento anterior a la apertura del HUD no se repite.
RefreshScore();
RefreshPlayerTable();
OnPhaseChanged(match.Phase);

// Al cerrar/destruir la interfaz:
tdm.ScoreChanged -= RefreshScore;
tdm.PlayerStatsChanged -= RefreshPlayerTable;
match.PhaseChanged -= OnPhaseChanged;
match.MatchFinished -= OnMatchFinished;
```

Las funciones `RefreshScore`, `RefreshPlayerTable` y otras son responsabilidad del HUD; no existen en los managers. Para vida y munición, encontrar los componentes del `PlayerObject` cuyo `IsOwner` sea verdadero y suscribirse allí, no en las copias remotas. Si ese objeto se recrea, desuscribirse del anterior y enlazar el nuevo.

Leandro puede escuchar `CountdownSecondChanged`, `PhaseChanged`, `DeathAnnounced`, `ScoreChanged` y `MatchFinished` para reproducir audio o efectos. `ServerDeathConfirmed` **no** es un evento de UI/audio: solamente se emite en el servidor para las reglas. `DeathAnnounced` es la notificación de presentación que reciben host y clientes y trae un `EventId` para reconocer duplicados. El receptor visual o de audio no debe volver a sumar eliminaciones.

No se envía el cronómetro por red cada frame. Los clientes calculan el tiempo visible con el reloj sincronizado de Netcode. Los jugadores todavía se identifican por ID; no hay nombres replicados.

## CanvasTDM integrado

`PlayerPrefab` referencia `CanvasTDM` desde `NetworkPlayerHudPresenter.matchHudPrefab`. Al aparecer el PlayerObject propietario en una sesión TDM/CTF, el presentador crea un canvas local en la escena de juego, separado de `PlayerUI`. No hace falta colocarlo manualmente en ArenaIndustrial ni en las escenas de prueba; tampoco se crea para jugadores remotos o para el entrenamiento individual. `PlayerUI` mantiene vida, armas y munición.

`MatchHudView` contiene las referencias visuales serializadas y `MatchHudPresenter` conecta los eventos de la sesión y el jugador. CanvasTDM muestra marcador y límite oficiales, tiempo sincronizado, dinero local, tres avisos de muerte durante cinco segundos y la tabla mientras se mantiene Tab. Las filas usan el ID estable y el equipo replicado. En CTF muestra capturas por equipo y estado de las banderas; sus estadísticas individuales no se publican y se muestran como `--`.

Al finalizar se muestra el resultado oficial y se libera el cursor. `VOLVER AL LOBBY` usa `TryReturnToLobbyAfterMatch`, sólo para el host, conservando la sala para configurar otra partida. `VOLVER AL MENU` cierra la conexión local antes de cargar MenuScene. El placeholder de votación de revancha se reemplaza por el flujo que existe actualmente.

El HUD se desuscribe y destruye al despawnear el propietario o cambiar de escena. La rutina de salida al menú conserva temporalmente su instancia para completar el cierre aunque Netcode destruya el jugador. Si la escena no tiene EventSystem, el HUD crea uno local para sus botones. El overlay de combate de prueba empieza oculto y continúa disponible con F8.
