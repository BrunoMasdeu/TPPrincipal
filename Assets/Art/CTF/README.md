# Bandera para captura la bandera

Asset visual creado en Blender 4.3.2 fuera del proyecto. Preparado y validado con Unity 6000.3.22f1 y URP 17.3.0.

## Uso

Arrastrar `Prefabs/CTF_Flag_Red.prefab` o `Prefabs/CTF_Flag_Blue.prefab` a la escena. El Animator reproduce automáticamente el ciclo de ondeo de 2 segundos. Los materiales y texturas ya están asignados.

El pivote está al pie del mástil; escala 1 corresponde a metros. Altura del mástil: 2,336 m. Ancho nominal de la tela: 1,10 m. El hijo `GripSocket` marca el agarre a 0,88 m del suelo. Para llevarla, se puede alinear ese punto con la mano del personaje.

## Contenido

- Dos FBX, uno por equipo, con 1.348 triángulos cada uno.
- Un mesh rígido para mástil y agarre; un mesh de tela con 7 huesos.
- Dos prefabs con Animator y controllers; clips `.anim` configurados en loop, que animan solamente los huesos de la tela.
- Materiales URP/Lit y dos texturas de color de 1024 × 1024. Tela visible por ambas caras.

La animación usa huesos y no necesita simulación Cloth. Los prefabs contienen solamente el asset visual y su animación. La recogida, caída, captura, colliders y sincronización de red se conectan desde la lógica CTF del juego.

Las bases circulares del render pertenecen a la presentación; no forman parte del asset.

## Fuente editable

El archivo `CTF_Flags.blend`, con texturas empaquetadas, queda fuera del repositorio:

`C:/Users/bruda/.codex/visualizations/2026/10/12/01a13192-7cb1-7510-b548-81b5da66b499/CTF_Flags.blend`

Las colecciones `CTF_Red` y `CTF_Blue` contienen los modelos. `Presentation_ONLY` contiene cámara, luces y pedestales de muestra. Los FBX se exportaron con el origen en (0,0,0), eje Y arriba y escala en metros.
