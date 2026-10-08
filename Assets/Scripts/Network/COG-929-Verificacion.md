# COG-929 — evidencia de Fase 1 pendiente de Unity

La implementación C# compiló con `dotnet build Game.csproj --no-restore` sin errores ni advertencias. No se ejecutó Test Runner ni se verificó ningún flujo en Unity, por pedido del usuario. Este archivo es la lista de comprobaciones para cerrar F1-308, F1-406, F1-512/513, F1-608, F1-714, F1-807/809 y F1-901/908. Dejar los casilleros vacíos hasta observarlos realmente.

## Pruebas automáticas manuales

- [ ] EditMode: suite completa, incluidas reglas de lobby, configuración, spawn, datos, `MatchStateRules` y carrera.
- [ ] PlayMode: suite completa y comparación con la línea base previa a COG-929.

## Escenario multijugador Relay

1. Abrir `MenuScene` en host y cliente con versiones iguales del proyecto (por ejemplo, Editor y build actualizada).
2. Elegir TDM, 2 jugadores; crear sala y unir el cliente. Repetir con CTF. Los selectores provisionales aparecen en la esquina superior derecha.
3. Seleccionar un rojo y un azul, marcar ready en ambas instancias y pulsar Start Game en host. El lobby debe mostrar `PLAYERS: 2/2`.
4. Confirmar que ambos modos cargan la escena indicada por su `GameModeDefinition` (`tdm_scene_prueba` por ahora), sin cargar escenas desde `MenuManager`/`ConnectionManager`.
5. Confirmar un `PlayerObject` por cliente, ownership correcto y punto del equipo correspondiente. Repetir callback/carga si se dispone de una prueba de PlayMode.
6. Confirmar que host y clientes observan Lobby → Loading → InMatch y WaitingForPlayers → Countdown → Playing → Finished, con cronómetro concordante.
7. Intentar desde un cliente equipo inválido, ready sin equipo, comienzo no autorizado y cambios de fase: deben rechazarse sin divergencia.
8. Cancelar durante conexión o carga, volver al menú y crear una segunda sesión. No deben quedar raíz, jugadores ni callbacks duplicados.

- [ ] TDM con Relay y 2 jugadores.
- [ ] CTF con Relay y 2 jugadores (sólo arquitectura, no reglas de bandera).
- [ ] Cantidades 4, 6 y 8 validadas posteriormente, con equipos equilibrados y spawn suficiente.
- [ ] Rechazo de cambios no autorizados y cancelación.
- [ ] Segunda sesión limpia.

## Carrera individual y compatibilidad

1. Desde `MenuScene`, iniciar partida individual. Debe usarse `RaceGameModeDefinition`, host local y `NetworkLobbySession`; `MenuManager` no debe cargar `CentroEntrenamiento` directamente.
2. Confirmar spawn en `Respawn`, cuenta regresiva común, inicio de carrera y `RaceStatusUI` funcional.
3. Terminar una vez por `Llegada` y otra por timeout; comprobar motivo, ganador y UI antiguos.
4. Si es posible, repetir carrera host/cliente heredada y verificar que no hay inicio ni spawn doble.

- [ ] Carrera individual por llegada.
- [ ] Carrera individual por timeout.
- [ ] Carrera heredada host/cliente (si ese flujo sigue expuesto).

## Evidencia para Jira/PR

- Fecha, versión de Unity y plataforma: pendiente.
- Resultado de EditMode y PlayMode: pendiente.
- Capturas/logs de Relay, escena, equipos, ownership, fases y tiempo: pendiente.
- Resultado de carrera individual y regresión: pendiente.
- Defectos encontrados y correcciones: pendiente.

Límites conocidos: TDM y CTF aún no implementan kills, capturas, respawn, marcador ni ganador por puntuación; CTF usa temporalmente el mapa de TDM. El cierre por timeout del manager común es empate hasta que los modos agreguen sus reglas. La UI `OnGUI` es provisoria.
