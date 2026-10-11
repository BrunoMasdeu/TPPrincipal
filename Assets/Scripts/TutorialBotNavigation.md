# Bots del tutorial

En `CentroEntrenamiento`, los cinco componentes `movimientoLateral` comparten el
`BotPatrolArea` de la instancia de `Factory1Floor02` situada debajo de ellos.
La otra instancia del mismo prefab no forma parte del recorrido.

- Los bots eligen destinos alcanzables, rodean los obstáculos del NavMesh y hacen
  pausas breves. Comprueban el cuerpo completo antes de avanzar, también frente
  a objetos que aparezcan después del bake.
- El perímetro usa el BoxCollider del piso en sus coordenadas locales, con margen
  para el radio del bot. Se comprueban los destinos, los puntos del camino y cada
  desplazamiento.
- `bot2` y `bot3` saltan en el lugar unos 0,8 metros cada 4–8 segundos. Comprueban
  el espacio superior; los otros tres no saltan. No saltan obstáculos ni bordes.
- Se conservan `TakeDamage` y `OnTargetDestroyed`, utilizados por el arma y por
  el contador de bajas del tutorial. El Rigidbody es cinemático y el Animator
  no aplica root motion: el movimiento lo controla la navegación.

## Ajustes en Unity

En cada bot se pueden ajustar `Velocidad`, `Pause Duration`, `Turn Speed`,
`Can Jump`, `Jump Height`, `Jump Duration` y `Jump Interval`. En el piso se
puede ajustar `Edge Margin`; al seleccionarlo se dibuja su límite en cian.

Si se mueve el piso o se cambia la distribución de obstáculos fijos, ejecutar
**Tools > Tutorial > Configurar y regenerar navegación de bots**. El comando
guarda la configuración y regenera `BotPatrolNavMesh.asset`. La navegación está
precocinada, de modo que el juego no necesita leer los meshes para construirla
en tiempo de ejecución. Un obstáculo nuevo bloquea el avance mediante consultas
físicas; para que el planificador trace nuevos caminos alrededor de él, regenerar
la navegación.

## Verificación

En Test Runner ejecutar `TutorialBotConfigurationTests` (Edit Mode) y
`TutorialBotPatrolTests` (Play Mode). Incluyen perímetro rotado/escalado,
obstáculos, salto y aterrizaje, techo bajo, daño y recorrido en la escena real.
La prueba de integración desactiva temporalmente los sistemas ajenos a los bots
en la escena cargada para la prueba; no modifica la escena guardada.

Para comprobarlo visualmente, abrir `CentroEntrenamiento`, iniciar Play y observar
los cinco bots. Dispararles debe seguir completando la tarea del tutorial.
