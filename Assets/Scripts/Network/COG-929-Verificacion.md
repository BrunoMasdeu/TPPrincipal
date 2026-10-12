# COG-929 — evidencia de verificación de red

Este archivo registra las comprobaciones manuales realizadas en Unity y mantiene visibles las pruebas que todavía faltan. No marcar un punto como completo sin observarlo en Editor o build.

## Pruebas automáticas manuales

- [ ] EditMode: suite completa, incluidas reglas de lobby, configuración, spawn, datos, `MatchStateRules` y carrera.
- [ ] PlayMode: suite completa y comparación con la línea base previa a COG-929.

## Escenario multijugador Relay

1. Abrir `MenuScene` en host y cliente con versiones iguales del proyecto (por ejemplo, Editor y build actualizada).
2. Crear una sala todavía sin modo, cantidad ni mapa. Unir el cliente con el código generado y confirmar que la sala muestra `2/8` conectados sin impedir la entrada por falta de configuración.
3. Desde el host elegir TDM, 2 jugadores y Arena Industrial. El cliente debe ver esos valores replicados, pero no debe poder modificarlos.
4. Seleccionar un rojo y un azul, marcar ready en ambas instancias y pulsar Start Game en host. El lobby debe mostrar `PLAYERS: 2/2`.
5. Confirmar que ambos modos cargan el mapa elegido en la sala. TDM debe ofrecer `tdm_scene_prueba` y `ArenaIndustrial`; CTF debe ofrecer `ctf_scene_prueba` y `ArenaIndustrial`.
6. Confirmar un `PlayerObject` por cliente, ownership correcto y punto del equipo correspondiente. Repetir callback/carga si se dispone de una prueba de PlayMode.
7. Confirmar que host y clientes observan Lobby → Loading → InMatch y WaitingForPlayers → Countdown → Playing → Finished, con cronómetro concordante.
8. Volver al lobby: el código y los jugadores deben mantenerse. Cambiar a CTF, 4 jugadores y Arena Industrial; los ready anteriores deben quedar desmarcados y el inicio debe bloquearse indicando que faltan dos personas.
9. Intentar desde un cliente cambiar modo, cantidad o mapa, además de equipo inválido, ready sin equipo y comienzo no autorizado: todo debe rechazarse sin divergencia.
10. Cancelar la conexión y crear una sala nueva. No deben quedar raíz, jugadores ni callbacks duplicados.

- [x] TDM con Relay y 2 jugadores en Arena Industrial.
- [x] CTF con Relay y 2 jugadores en Arena Industrial, incluidas reglas de bandera.
- [ ] Cantidades 4, 6 y 8 validadas posteriormente, con equipos equilibrados y spawn suficiente.
- [ ] Rechazo de cambios no autorizados y cancelación.
- [x] Segunda sesión limpia y selección de mapa conservada al volver al lobby.
- [ ] Sala flexible: crear sin configuración, configurar dentro del lobby y reutilizar el mismo código para otra cantidad/modo.

## Carrera individual y compatibilidad

1. Desde `MenuScene`, iniciar partida individual. Debe usarse `RaceGameModeDefinition`, host local y `NetworkLobbySession`; `MenuManager` no debe cargar `CentroEntrenamiento` directamente.
2. Confirmar spawn en `Respawn`, cuenta regresiva común, inicio de carrera y `RaceStatusUI` funcional.
3. Terminar una vez por `Llegada` y otra por timeout; comprobar motivo, ganador y UI antiguos.
4. Si es posible, repetir carrera host/cliente heredada y verificar que no hay inicio ni spawn doble.

- [x] Carrera individual inicia sin requerir ready manual y conserva el flujo del tutorial.
- [ ] Carrera individual por llegada y timeout revalidada después de la integración.
- [ ] Carrera heredada host/cliente (si ese flujo sigue expuesto).

## Evidencia para Jira/PR

- Fecha, versión de Unity y plataforma: pendiente.
- Resultado de EditMode y PlayMode: pendiente.
- Capturas/logs de Relay, escena, equipos, ownership, fases y tiempo: pendiente.
- Resultado de carrera individual y regresión: pendiente.
- Defectos encontrados y correcciones: pendiente.

Límites conocidos: la UI `OnGUI`, los paneles de debug y la representación lógica de las banderas son provisorios. Falta repetir la validación con 4, 6 y 8 jugadores y ejecutar las suites completas de EditMode y PlayMode después de la integración.
