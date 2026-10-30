# Prototipo de exploración de Guille

La autorización del usuario para aplicar y terminar los cambios sustituye el flujo de aplicación manual del handoff. El jefe es provisional, según su elección explícita. Esta entrega completa el bucle de exploración; no sustituye el sistema de combate por cartas.

## Uso

Abrir `Assets/Scenes/SceneGuille/SceneGuilleDungeon.unity` y entrar en Play. Clic en el suelo para caminar; **E** junto a un objeto para interactuar. Encontrar la llave, abrir una puerta de las escaleras y bajar. Cada enemigo se resuelve individualmente en el modo de prueba.

`DungeonRunController` permite configurar habitaciones, crecimiento, límite, contenido, ambientación y probabilidad de jefe. Los valores iniciales son de prueba: 6–8 habitaciones, crecimiento de una por piso, límite 16; probabilidad inicial 10 %, incremento 8 puntos porcentuales y tope 65 %.

La tirada se realiza una sola vez al recoger la llave y se conserva en `DungeonRunState`. El piso de jefe usa la misma generación BSP, contiene un encuentro marcado como provisional y exige resolverlo, recoger la llave y abrir la salida. El siguiente piso es normal. El reinicio de probabilidad tras el jefe es configurable y está activado inicialmente. No hay guardado en disco.

## Cómo está organizado

- `DungeonPopulation` copia el piso estructural, coloca contenido con una semilla independiente y comprueba accesos antes y después de abrir las puertas. La decoración respeta las reservas de circulación.
- `DungeonContentView` representa objetos y colisiones; valida además los accesos con el volumen físico del avatar.
- `DungeonRunController` coordina las interacciones y prepara el siguiente piso antes de sustituir el actual. Un fallo recuperable conserva el anterior.
- `DungeonRunState` guarda únicamente datos de exploración, sin referencias a objetos de la escena que se destruyen.
- `DungeonAtmosphere` mantiene la antorcha del jugador, encendido de antorchas de pared con comprobación de obstrucción y niebla tenue. Como máximo activa cuatro luces de pared cercanas; el estado de encendido persiste durante el piso.
- `DungeonMaterialScope` crea variantes locales de materiales y las elimina con el piso. `GuilleWorldStyle.asset` es un perfil independiente. La habilitación en `WorldStyleGate` se limita a añadir esta escena.

Piensa en la transición como preparar una maqueta nueva en otra mesa: primero se monta y se comprueba que el personaje cabe; después se retira la maqueta anterior.

## Integración futura de combate y recompensas

Con `enablePrototypeEncounterResolution` activo, E resuelve el encuentro de prueba. Para conectar combate real, desactivarlo, suscribirse a `EncounterRequested` y llamar a `ResolveEncounter(id)` al resolverlo. `InteractionCompleted` comunica otras interacciones. Cofres, curación, mejora y evento son puntos de interacción de prueba: no cambian vida, cartas ni economía. Así no se duplican reglas del juego todavía no integradas con esta escena.

## Validación reproducible

`Assets/Editor/GuilleDungeonValidation.cs` proporciona los menús **Tools > Guille Dungeon** para configurar y validar datos. El puente local acepta solicitudes en `artifacts/guille-dungeon/request.json`, exclusivamente con la escena de Guille activa. Es una alternativa de validación cuando MCP no está disponible, no forma parte del juego compilado.

Comandos: `setup`, `refresh`, `validate`, `tests`, `play`, `stop`, `status`, `smoke`, `large`, `capture_base`, `capture`. Esperar el resultado y la finalización de cada operación antes de enviar otra. `smoke` requiere Play: fuerza temporalmente la probabilidad de jefe y acelera al avatar para recorrer tres descensos con navegación real; salir de Play restaura los valores de escena. `large` reinicia temporalmente la partida con 16 habitaciones para revisar el máximo configurado. `refresh` importa cambios de scripts sin depender del foco de la ventana.

Los resultados se escriben en `data-validation.txt`, `runtime-validation.txt`, `world-style-tests.xml` y capturas PNG dentro de `artifacts/guille-dungeon`. La validación de datos comprueba 24 semillas, determinismo, aislamiento del piso original, ocupación, llave, bloqueo de escaleras, accesos y estado de jefe. Las capturas de cámara no incluyen el HUD; `game-view.png` sí lo incluye y puede mostrar gizmos del Editor.

## Límites del prototipo

El jefe usa una representación y resolución de prueba. No se implementan aquí combate, recompensas definitivas, guardado ni WFC. Decoración e interacciones usan huellas de una casilla. Los contadores de render se recogen en el Editor y no acreditan el presupuesto de una build final; queda pendiente el perfilado en hardware objetivo. Los menús históricos del probe orientados a distancias estructurales deben usarse antes de poblar el piso, ya que las puertas y objetos cambian su transitabilidad.

## Evidencia de esta entrega

- Datos: 24 semillas válidas, sin fallos de generación en la muestra.
- Edit Mode: 17 tests de `WorldStyleGateTests` aprobados.
- Play Mode: recorrido de tres descensos, incluyendo jefe, y comprobación de limpieza de contenido, ambientación y antorcha del jugador.
- Capturas base/final inspeccionadas; corregidas la niebla con bordes cuadrados y la claridad excesiva del fondo.
- Piso de 16 habitaciones generado y capturado, con validación física de accesos. El contador del Editor llegó a 5.889 draw calls y 883.703 triángulos en la captura. Estos contadores pueden incluir cámaras del Editor; el tiempo registrado en segundo plano está afectado por su limitación de actualización y no permite concluir FPS de juego. El rendimiento de build sigue pendiente.
- Compilación final sin errores y recorrido final sin excepciones observadas en el registro. Existe el warning previo CS0108 de `HexCellVisual.renderer`; el registro conserva además los errores de sombras y pruebas interrumpidas anteriores a su corrección.
- No se modifican `SceneVictor` ni `SceneVictorLab`. No se ha creado ningún commit.
