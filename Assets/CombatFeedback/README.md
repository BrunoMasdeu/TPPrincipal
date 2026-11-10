# Feedback de disparos

- En TDM/CTF, el servidor emite `NetworkPlayerCombat.HitConfirmed` al atacante después de aplicar daño efectivo. El punto común incluye armas, cuchillo y granadas. Aliados, blancos muertos y daño rechazado no generan confirmación.
- El HUD local crea `HitConfirmationFeedback`: cuatro trazos blancos durante 0,12 s para impactos; trazos más largos, gruesos y cian durante 0,30 s para bajas. La baja tiene prioridad sobre impactos posteriores. No altera la mira ni la cámara.
- El HUD sintetiza un clic corto de impacto y un tono más grave de baja. Los sonidos son 2D y respetan el volumen general del juego. En el Inspector del prefab `PlayerUI`, el componente `Hit Confirmation Feedback` permite cambiar `Volume` para ambos sonidos, ajustar sus duraciones y asignar clips propios. Ambos equipos comparten este prefab.
- Los impactos del escenario generan partículas locales, sin audio de confirmación. Se eligen chispas cuando el material del renderer tiene `_Metallic >= 0.5`; en otro caso, polvo. Para superficies con mapas metálicos o materiales mixtos, agregar `SurfaceImpactFeedback` al collider o un padre y elegir explícitamente Dust/Metal.
- Partículas y marcas temporales se eliminan automáticamente. El material de partículas está incluido por Resources para conservar su shader en builds.
- Los bots y zombies conservan su sistema de daño actual; la confirmación de daño de este cambio corresponde a rivales del multijugador.

Validación: `HitConfirmationFeedbackTests` y `MatchHudFlowTests.DamageConfirmationRejectsAlliesAndDeadTargetsAndReachesOwnerHud` (ambos equipos).
