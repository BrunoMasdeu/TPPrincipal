# Wallrun del PlayerPrefab

Fuente editable: `PlayerWallRun.blend` (Blender 5.2 LTS).
Acciones: `WallRunLeft` y `WallRunRight`, 25 fotogramas a 30 FPS,
con el último fotograma repitiendo el primero (ciclo de 0,8 segundos).
Se usa el esqueleto original de `Mark_1_Olive_IdlePose.fbx`.

## Edición y exportación

Seleccionar `Armature.001` y abrir el Action Editor para elegir una acción.
Las claves están horneadas sobre el rig original; no se requiere un complemento.
`Tools/Animation/create_wallrun.py` reproduce la autoría y exporta ambos FBX.
Ejecutarlo vuelve a generar el `.blend`: guardar las modificaciones manuales
con otro nombre antes de regenerar.

En Unity, `Tools > Player > Build Wall Run Animations` configura la importación
Humanoid usando el Avatar del soldado y construye la capa `Wall Run`.
Los FBX están en `Assets/Animations/WallRun`.

## Armas y movimiento

Orden de capas: `Base Layer`, `Wall Run`, `Upper Body`.
El wallrun aporta piernas y pose sin armas; `Upper Body` conserva las poses,
el disparo y la recarga existentes de pistola y rifle. No se sustituye el
controlador ni se duplican las animaciones de combate.

`PlayerAnimationController` mezcla el wallrun en 0,14 segundos, ajusta la
cadencia a la velocidad y replica el lado mediante una NetworkVariable del
propietario. El salto desde la pared termina el wallrun inmediatamente.
El Rigidbody sigue controlando el desplazamiento y la cámara no recibe roll.

Los pies describen apoyos de una pared lateral ideal a unos 46 cm del centro
del rig. No hay IK de contacto dinámico: la coincidencia exacta con paredes a
distintas distancias y ángulos requiere revisión en el nivel y, si se desea,
una etapa de IK. La vista de primera persona debe revisarse jugando, porque
el encuadre depende del arma y de la cámara del prefab.

## Verificación

`WallRunAnimationTests`: 5 pruebas de EditMode verifican importación Humanoid,
continuidad del ciclo, orden/máscara de capas y disparo/recarga con pistola y
rifle durante wallrun. No equivalen a una sesión de juego multijugador.

`WallRunAnimationSetup.RenderPreviews` genera vistas del prefab con las capas
combinadas. `Tools/Animation/preview_wallrun.py` genera vistas del ciclo sin armas
en Blender. Las imágenes son comprobaciones estáticas de poses.
