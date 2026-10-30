# Traspaso: mazmorra de habitaciones y pasillos para Guille

Fecha: 8 de octubre de 2026.

Este documento sirve como prompt autónomo para continuar el trabajo en otra conversación. Describe el objetivo aprobado, las restricciones, los antecedentes y un plan de implementación. **No describe una implementación ya terminada.** El siguiente agente debe contrastar los antecedentes con los archivos actuales antes de proponer cambios.

## Instrucción para la siguiente conversación

Actúa como un profesor de desarrollo de software paciente y didáctico. Ayúdame a implementar este plan por etapas, explicando las decisiones y proporcionando código coherente con el proyecto real. No te limites a volver a redactar el plan: comienza por inspeccionar el estado actual y preparar el primer bloque implementable. Al terminar cada explicación, haz una pequeña pregunta o plantea un mini-reto para comprobar mi comprensión.

**No crees ni edites directamente archivos C# del proyecto, incluidos scripts de Editor y pruebas.** Dame el código y las instrucciones para que lo aplique yo. Para archivos nuevos, indica ruta, contenido completo, propósito y dependencias; para archivos existentes, indica exactamente qué añadir o sustituir y dónde. No uses ejecución de C# mediante MCP como vía alternativa para implementar lógica que yo debo aplicar.

Sí puedes trabajar en recursos visuales, shaders, materiales, modelos, prefabs y configuración de la nueva escena utilizando Unity MCP y Blender MCP cuando corresponda. Reutiliza los recursos existentes. Las operaciones visuales deben respetar las restricciones de escenas y recursos compartidos de este documento.

No presupongas que he aplicado un bloque de código. Antes de avanzar a otro bloque que dependa de él, comprueba los archivos y la compilación o espera mi confirmación. Continúa entretanto con tareas independientes autorizadas. No entregues toda la implementación como una enorme lista de scripts sin comprobar sus dependencias.

## Alcance y restricciones persistentes

- Proyecto: `C:\Users\USUARIO\Desktop\Dungeon_Run`.
- Proyecto de referencia, de solo lectura: `F:\TFG_TownHeroRPG\TownHeroRPG_TFG`.
- Crear el experimento en una escena nueva dentro de `Assets/Scenes/SceneGuille/`. Nombre propuesto: `SceneGuilleDungeon.unity`; comprobar antes si ya existe.
- No modificar `SceneVictor` ni `SceneVictorLab`. Esta es una excepción explícita del usuario a la convención de escena de laboratorio del repositorio.
- Conservar `SceneGuilleMaze`, `SceneGuilleMaze 2`, `SceneGuilleHex` y los demás prototipos.
- No modificar materiales, prefabs, shaders o perfiles compartidos de manera que cambie indirectamente la apariencia de las escenas de Victor. Crear variantes locales cuando haga falta.
- No importar todo TFG ni sobrescribir clases existentes. Extraer y adaptar las partes útiles, preservando GUID y referencias de Unity.
- No editar `Library`, `Temp`, `Logs`, `obj`, `.csproj` ni `.sln` generados. No descartar cambios ajenos ni hacer commits sin petición.
- No crear subagentes ni otras conversaciones salvo que el usuario lo solicite.
- Leer `AGENTS.md` y los documentos pertinentes. Las instrucciones explícitas del usuario prevalecen sobre las convenciones generales.
- La referencia a Enter the Gungeon describe la organización espacial de salas y pasillos. No implica copiar recursos, reproducir su algoritmo interno ni sustituir el combate de cartas por combate de acción.
- Conservar el estilo acrílico/pictórico de Victor y adaptar la atmósfera oscura a él. No convertir el proyecto en PBR genérico por interpretar «relativamente realista» de forma aislada.

## Resultado deseado

1. Varios espacios amplios interconectados mediante pasillos.
2. Habitaciones basadas en presets de formas primitivas: rectangulares, cuadradas y circulares.
3. Decoración de suelo y paredes, combinando elementos fijos y colocación procedural.
4. Habitaciones vacías o con uno o varios enemigos. «Vacía» puede tener decoración: significa sin encuentro u objeto especial.
5. Habitaciones de cofre o evento con exactamente un objeto principal central, además de la decoración compatible.
6. Habitaciones de curación y mejora, con un punto principal de interacción en la primera versión.
7. Habitación de escaleras bloqueada inicialmente; una llave del mismo piso permite abrirla.
8. Descender reemplaza el escenario por otro piso generado.
9. La progresión incrementa por ahora el intervalo del que se elige la cantidad de habitaciones. Más habitaciones aumentan recorrido y exposición; no equivalen automáticamente a enemigos más fuertes.
10. Recoger la llave realiza una tirada para determinar si el siguiente piso es de jefe. La probabilidad aumenta con la progresión y tiene un límite configurable. El resultado se guarda y no se vuelve a sortear al interactuar.
11. Iluminación oscura y opresiva, con la antorcha del personaje como fuente principal.
12. Antorchas apagadas en paredes que se encienden al acercarse el jugador y permanecen encendidas durante la estancia en el piso.
13. Niebla de partículas tenue y ambiental. Ya no debe ser una barrera de descubrimiento que oculte bloques enteros.
14. Conservar provisionalmente movimiento por clic y avatar corriendo. El usuario todavía puede elegir control directo; no bloquear la generación por esa decisión.

## Estado anterior que debe verificarse

En `Assets/Scripts/PCG/` existen:

- `RandomWalkWFC.cs`: modo hexagonal y modo cuadrado; en el cuadrado usa `MazeLayout` como geometría y aplica WFC a tipos de celda.
- `MazeLayout.cs`: cuadrícula completa de celdas, conexiones mediante DFS aleatorio y algunas conexiones extra. No representa todavía salas amplias con pasillos independientes.
- `MazeMapView.cs`: genera suelos, paredes, contenido y niebla del prototipo.
- `MazeCellView.cs`: estado visual y de descubrimiento, contenido central, puntos de recorrido e interacción de prueba.
- `MazeNavigator.cs`: navegación por clic, recorrido del avatar, descubrimiento e interacción E. La implementación previa rodeaba el centro cuando había un obstáculo y usaba recorridos directos en celdas vacías.
- `FogParticles.cs`: cobertura por puntos y emisión de partículas; se reutilizó en la escena duplicada para sustituir la niebla de shader.

La E sobre cofres/enemigos funcionaba y permitía eliminar contenido como prueba. Hubo un error de referencia: se había asignado el prefab de la etiqueta en vez de su instancia en escena. Comprobar referencias concretas al preparar la nueva escena.

Recursos reutilizables:

- `Assets/Art/Environment/MazePrototype/`: modelos, prefabs, materiales, shaders y animaciones del kit previo.
- Suelos y paredes modulares, pilares, cofre, altar y avatar provisional con Idle/Run.
- `Assets/Materials/TrigonalAbyss/AcrylicV3`: referencia de materiales existentes, sin alterar recursos compartidos indiscriminadamente.
- `docsBlender/MazePrototype/MazeKit.blend`: fuente Blender previa; comprobar disponibilidad.
- `Assets/Scripts/Rendering/DungeonRunFlameLight.cs`: comportamiento existente de luz de llama.
- `Assets/Scripts/Rendering/Core/WorldStyleGate.cs`: comprobar cómo permite el estilo en cada escena. Si necesita cambios C#, proporcionar instrucciones al usuario.

El movimiento, la vista, la niebla y WFC están parcialmente acoplados al concepto anterior de una habitación por celda. Reutilizar sus piezas útiles no significa conservar ese acoplamiento.

## Lecturas e inspección inicial

Consultar según la fase: `docs/GAME_DESIGN.md`, `docs/PROJECT_ARCHITECTURE.md`, `docs/ART_DIRECTION.md`, `docs/SHADER_GUIDE.md`, `docs/TESTING_GUIDE.md`, `docs/PERFORMANCE_BUDGET.md` y `docs/DECISIONS.md`.

La identidad documentada incluye combate de cartas por turnos y una mazmorra triangular. Para este experimento, el usuario ha pedido expresamente salas de formas primitivas con pasillos. No extrapolar esa decisión a un cambio de todo el juego ni modificar sus reglas de combate.

Inspeccionar el diff y estado del proyecto, las clases de PCG, escenas y recursos antes de proponer una integración. Leer la habilidad Unity MCP antes de usarla. Comprobar conexiones MCP y proyecto activo; no asumir que los identificadores de instancias de otra conversación siguen vigentes.

## Qué aprovechar de TFG_TownHeroRPG

Ruta base de los scripts: `Assets/Scripts/PCG/`, dentro del proyecto de F:.

| Área | Archivos de referencia | Uso previsto |
| --- | --- | --- |
| Distribución | `DungeonMainCustom/DungeonAlgorithms.cs`, `BinarySpacePartitioningGenerator.cs` | Adaptar BSP y construcción de formas; separar Tilemaps de los datos. |
| Alternativa de geometría | `DungeonMainCustom/RandomWalkCorridorGenerator.cs` | Referencia para una variante futura; no implementar dos generadores en la primera entrega. |
| Contenido | `RoomGenCustom/Room.cs`, `DungeonData.cs`, `DungeonContentGenerator.cs` | Entender datos de habitación y asignación de tipos. |
| Decoración | `RoomGenCustom/PrefabPlacer.cs`, `Prop.cs` y parámetros asociados | Adaptar categorías de colocación, tamaños y cantidades a prefabs 3D. |
| Accesibilidad | `RoomGenCustom/RoomGraph.cs` | BFS sobre suelo libre, ampliado con validación de destinos obligatorios. |
| Distancias | `DecisionItems/Dijkstra.cs` | Reutilizar el concepto; comprobar si basta BFS de coste uniforme. |
| WFC | `WFC Def/AdvancedWFC.cs` y tipos relacionados | Referencia opcional futura; evitar colisiones de nombres con el proyecto actual. |

Hallazgos de la revisión anterior que deben tenerse en cuenta:

- BSP puede cortar fuera de los límites que garantizan tamaños mínimos de ambos hijos; después descarta regiones pequeñas. Corregir las condiciones y el intervalo de corte.
- La conexión actual de centros en BSP forma una cadena por elección del siguiente centro cercano; no equivale a un árbol mínimo con ramas.
- Sus corredores van entre centros, primero por un eje y después por otro. Las intersecciones o pasos por terceras habitaciones pueden crear conexiones físicas no previstas.
- En la variante de salas por Random Walk se guardaba suelo sin recortar aunque parte de la representación sí se recortaba. Unificar datos y geometría.
- Hay una validación repetida de altura que omite anchura. No portar fallos conocidos.
- La asignación de salas ordena distancias; un valor de inalcanzable no puede tratarse como «la sala válida más lejana».
- La clase llamada Dijkstra trabaja con vecinos de coste uno y una cola; para distancias uniformes, BFS explícito es suficiente. Si se añaden pesos, usar una implementación apropiada de Dijkstra.
- `RoomGraph` utiliza ocho direcciones sin impedir cortes de esquina. Ajustar adyacencia y anchura libre al movimiento real del avatar.
- `PrefabPlacer` coloca decoración antes de ejecutar BFS. BFS obtiene posiciones accesibles para jugador, enemigos y objetos especiales; no repara la decoración ni verifica todas las puertas por sí solo.
- En BSP, `dungeonData.Path` contiene corredores entre centros, incluyendo tramos interiores. Se excluye de la colocación de decoración; NO se excluye del BFS. Los objetos especiales pueden ocupar ese recorrido.
- No confundir `PropPlacementManager`, cuyo contenido estaba comentado, con el colocador activo.
- Sustituir mezclas aleatorias basadas en `Guid.NewGuid` por un RNG con semilla; no depender del orden de un HashSet para decisiones reproducibles.
- Hay tipos y componentes 2D: SpriteRenderer, Collider2D, Tilemaps y coordenadas XY. Adaptar a XZ y a los recursos 3D del proyecto actual.

## Arquitectura propuesta

Mantener dos niveles: grafo de habitaciones para organización y progresión; cuadrícula de suelo para construcción, ocupación y navegación.

Nombres orientativos, sujetos a la estructura existente:

- `DungeonGenerationSettings`: límites, cantidad de salas, distribución de tipos y configuración de conexiones.
- `RoomPreset`: definición inmutable de forma, puertas candidatas, anclas y zonas de colocación.
- `DungeonFloorData`: datos del piso, salas, corredores, semilla, tipo de piso y resultados de validación.
- `RoomData`: identificador estable, preset, transformación, máscara de suelo, puertas seleccionadas, tipo y estado.
- `CorridorData`: extremos, recorrido, anchura y conexiones físicas.
- `DungeonRunState`: progresión y resultados persistentes entre pisos; integrar con estado existente si lo hay.
- Servicios separados para distribución, conexiones, asignación de tipos, contenido, validación y presentación.

No es obligatorio crear una clase por cada término. Evitar una clase monolítica y también interfaces o jerarquías sin uso real. Los datos no deben depender de Camera, Animator, ParticleSystem ni referencias a objetos vivos. No mutar ScriptableObjects para almacenar estado de partida.

Usar una semilla maestra y flujos reproducibles separados para geometría, asignación de salas, encuentros, decoración y progresión. Derivar semillas mediante una función estable; evitar hashes de cadenas no estables entre procesos. Ordenar candidatos antes de decisiones aleatorias si provienen de colecciones sin orden garantizado.

## Plan de implementación

### 1. Contratos y primer prototipo

Definir unidades, coordenadas XZ, tamaño de celda, anchura mínima de puertas/pasillos, radio del avatar y representación de ocupación. Asegurar que datos, colliders y navegación usan las mismas convenciones.

Primera configuración: piso normal pequeño, sin decoración, tipos especiales todavía representados con marcadores de depuración. Mantener seguros los prototipos anteriores mediante componentes nuevos o adaptaciones compatibles.

**Aceptación:** datos serializables o inspeccionables, semilla explícita y responsabilidades definidas. Código aplicado por el usuario y compilando antes de crear componentes dependientes.

### 2. Presets híbridos de habitaciones

Crear primero tres formas: cuadrada, rectangular y circular. La forma circular puede usar inicialmente una máscara sobre cuadrícula; el contorno visual se refinará después.

Cada preset debe describir:

- Dimensiones y casillas de suelo.
- Puertas candidatas con orientación y anchura.
- Punto central y zona de interacción.
- Decoración arquitectónica fija, si procede.
- Zonas para decoración variable: esquinas, paredes e interior.
- Anclas de antorchas.
- Espacios reservados y tipos de sala compatibles.

Las puertas candidatas que no se usen deben quedar cerradas. Si un preset se rota, rotar también su máscara, anclas, huellas y orientación de puertas. Validar la transitabilidad de los elementos fijos del preset.

**Aceptación:** los tres presets se pueden representar individualmente, con puertas y colliders coherentes y el avatar cabe en sus accesos.

### 3. Distribución mediante BSP

Elegir primero una cantidad objetivo dentro del intervalo del piso. Dividir regiones que admitan dos hijos válidos hasta alcanzar el objetivo o una condición explícita de inviabilidad. Escoger presets compatibles con cada región y dejar márgenes para paredes y corredores.

Reintentos limitados y reproducibles. Si no cabe la cantidad solicitada, seguir una política explícita: intentar otra distribución, ampliar el área dentro de límites o informar del fallo. No reducir silenciosamente cantidades obligatorias.

No usar Random Walk para deformar las salas en esta primera versión. Su conservación en prototipos antiguos no significa que deba intervenir en el nuevo generador.

**Aceptación:** habitaciones sin solapamiento, presets dentro de sus regiones y cantidad conforme a la configuración o fallo explicado.

### 4. Grafo conectado y corredores físicos

Crear candidatos entre habitaciones próximas, obtener un árbol de expansión mínima y añadir algunas aristas extra para producir ciclos. Garantizar que el conjunto de candidatos es conectable; ampliar la búsqueda si hace falta.

Elegir puertas compatibles y construir pasillos entre ellas, inicialmente rectos o con un giro. Comprobar anchura, colisiones y espacio respecto de otras salas. Si una ruta simple falla, probar alternativas limitadas o incorporar búsqueda de caminos.

No dar por completada una conexión abstracta si no se puede construir físicamente. Evitar cruces accidentales o representarlos como conexiones reales, incluyendo sus efectos en distancias y bloqueos. Si se introducen uniones de pasillo, modelarlas cuando haga falta para que el grafo refleje la transitabilidad.

El DFS del laberinto cuadrado deja de ser el constructor principal. Puede seguir existiendo en su prototipo; no es necesario añadir DFS después de obtener una red conectada salvo para un propósito concreto de recorrido o validación.

**Aceptación:** todo el piso está conectado antes de aplicar bloqueos; los datos de conexiones coinciden con el suelo real.

### 5. Representación y navegación

Construir suelos, paredes, huecos de puerta y colliders a partir de datos. Reutilizar el kit existente cuando su escala lo permita.

Conservar provisionalmente clic para elegir destino y A* sobre cuadrícula transitable para calcular ruta. Reutilizar ejecución del movimiento, giro, Idle/Run y seguimiento de cámara. Sustituir los rodeos fijos alrededor del centro por navegación basada en obstáculos reales.

Considerar el radio del personaje mediante márgenes u ocupación ampliada; impedir diagonales que atraviesen esquinas. Suavizar rutas únicamente cuando el volumen del avatar tenga paso libre. Destinos inválidos no deben provocar teletransporte ni atravesar paredes.

Emitir eventos de entrada/salida de habitación. Definir qué ocurre si se hace otro clic durante el movimiento, de forma consistente con el controlador existente. Actualizar ocupación y rutas al retirar obstáculos o cambiar puertas.

**Aceptación:** recorrer el piso vacío con animación de carrera y sin atravesar geometría. Una sala despejada permite movimientos directos cuando sean físicamente posibles.

### 6. Asignación de tipos y posible WFC futuro

Calcular distancias desde el inicio: BFS con costes uniformes; Dijkstra si se ponderan longitudes de pasillos u otros costes. El algoritmo mide; las reglas de diseño deciden el tipo de habitación.

Primera versión con reglas explícitas y selección aleatoria entre candidatos válidos:

| Tipo | Criterio inicial |
| --- | --- |
| Inicio | Aparición segura y espacio suficiente. |
| Escaleras | Alejada y con una entrada controlable, preferiblemente en una hoja del grafo. |
| Ubicación de llave | Alcanzable con escaleras cerradas, preferiblemente en otra rama. No exige un tipo de habitación exclusivo. |
| Cofre | Favorecer desvíos opcionales y disponer de centro accesible. |
| Curación | Reparto según distancias y ritmo de recorrido, configurable. |
| Mejora | Distribución que evite concentración excesiva. |
| Evento | Elección entre presets y ubicaciones compatibles. |
| Combate/vacía | Completar el resto según pesos y límites configurados. |

Garantizar inicio, escaleras y llave en pisos normales. Definir prioridades y cantidades opcionales cuando haya pocas salas. Si se exige una sala de escaleras con una sola entrada, preservar un candidato adecuado al añadir ciclos o ajustar la topología antes de fijar la asignación.

**Extensión futura explícita: WFC podría añadirse como un paso extra de asignación para aumentar la complejidad de las relaciones entre tipos de habitación. NO implementarlo en la primera versión.**

Una posible ampliación sería usar las habitaciones como nodos, sus tipos permitidos como dominios y reglas de compatibilidad entre vecinas: evitar dos curaciones contiguas, limitar ciertas secuencias o exigir vecindades compatibles con un tema. Se fijarían las asignaciones obligatorias, se filtrarían dominios por preset y distancia, y se propagarían restricciones antes de colapsar las habitaciones pendientes.

Ese WFC trabajaría sobre el grafo de salas, que no tiene necesariamente cuatro vecinos por nodo. Adaptar el modelo del WFC actual; no copiar sus supuestos de cuadrícula sin revisarlos. No añadir una segunda pasada que sobrescriba sin control las asignaciones ya decididas.

WFC no sustituiría BSP, la construcción de corredores, BFS/Dijkstra ni las comprobaciones de llave y escaleras. Las restricciones globales, cantidades exactas y alcanzabilidad necesitan tratamiento explícito o validadores adicionales. Una futura implementación requeriría gestión de contradicciones y reintentos acotados, preservando asignaciones obligatorias.

**Aceptación inicial:** tipos asignados con reglas y aleatoriedad controlada, sin dependencia de WFC. Dejar documentado dónde podría incorporarse la ampliación, sin crear un sistema especulativo completo.

### 7. Contenido y decoración con validación BFS

Orden lógico: asignar tipo, reservar contenido obligatorio y accesos, colocar decoración, validar conectividad y seleccionar posiciones finales válidas para el contenido restante.

Reservar puertas, anchura de paso, rutas necesarias entre accesos y espacio de aproximación a interactuables. Un objeto central bloqueante necesita una posición accesible desde la que interactuar; no exigir que su propia casilla sea transitable.

Adaptar las reglas de TFG a prefabs 3D: paredes, esquinas, interior, huella de tamaño, cantidad, orientación y agrupaciones. Colocar primero elementos grandes. Diferenciar bloqueo de navegación, reserva para otro objeto y decoración no bloqueante.

Tras decorar, ejecutar BFS desde una entrada con las mismas reglas de tránsito del avatar. Comprobar todas las puertas y posiciones de interacción obligatorias. Si falla, retirar/recolocar objetos con intentos limitados y una alternativa segura sin decoración bloqueante problemática.

Enemigos: cero, uno o varios en salas compatibles, sin solapamiento y en posiciones válidas. Cofres/eventos: exactamente un objeto principal central. Curación/mejora: un punto principal en esta versión. No permitir que la colocación posterior invalide el acceso a contenido obligatorio.

WFC para patrones decorativos sería otra ampliación opcional, distinta de la propuesta del punto 6. Tampoco es requisito inicial.

**Aceptación:** variedad sin bloquear accesos, contenido obligatorio alcanzable y ausencia de solapamientos indebidos.

### 8. Interacciones, llave y bloqueo de escaleras

Separar cada interactuable del estado de toda la habitación. Una sala con varios enemigos no debe perder todo su contenido al interactuar con uno.

Conservar la E como indicación de una interacción válida: distancia, estado y objetivo elegible. Si hay varios candidatos, elegir de forma estable y comprensible. Comprobar que se referencia la instancia de etiqueta adecuada.

La desaparición de un enemigo mediante E sigue siendo una herramienta de prueba, no la implementación del combate final. Integrar encuentros mediante los sistemas del proyecto sin inventar nuevas reglas de cartas.

Primera llave: recogida directa garantizada. Estado de puerta cerrada/abierta representado tanto en navegación como en colisiones. La llave solo debe obtenerse una vez. Definir si se consume al abrir, manteniendo coherente el estado del piso.

Validar con puerta cerrada que se alcanza la llave y, tras abrir, que se alcanzan las escaleras. Comprobar que ningún corredor alternativo permite saltarse el bloqueo.

**Aceptación:** recoger llave, abrir puerta y acceder a escaleras; eliminar un obstáculo de prueba habilita el paso correctamente.

### 9. Progresión, cambio de piso y jefe

Integrar estado persistente con el existente, sin duplicar sistemas de jugador o partida. El estado de piso incluye semilla, número, dificultad, llave, puerta y tipo de siguiente piso. No almacenar referencias vivas del escenario que va a destruirse.

Cantidad de habitaciones: sorteo entre mínimo y máximo definidos por progresión, con límites de tamaño y rendimiento. Todos los números iniciales serán parámetros de prueba, no reglas finales de balance.

Probabilidad de jefe: fórmula configurable con inicio, incremento y tope. Evaluar una vez al recoger la llave, usando el flujo RNG de progresión, y guardar el resultado antes de mostrarlo o permitir descender.

Transición: impedir doble activación, suspender entrada de movimiento, generar/validar el siguiente piso, sustituir el anterior, colocar al jugador y reanudar. Si falla la generación, conservar un estado recuperable; no dejar la partida sin piso válido. Limpiar eventos, partículas, luces registradas y referencias del piso anterior.

Decisiones pendientes antes de implementar el flujo de jefe: estructura del piso de jefe, condición de salida, existencia de llave allí y reinicio o acumulación de probabilidad tras superarlo. Mantener esa configuración separada; no inventar silenciosamente las reglas. Puede usarse un escenario mínimo de prueba claramente identificado hasta decidir el contenido final.

No implementar guardado en disco completo salvo que el alcance lo requiera o ya exista. Si hay guardado existente, integrar los nuevos datos para que cargar no repita la tirada de jefe.

**Aceptación:** varios descensos seguidos sin perder el estado del jugador ni acumular objetos o suscripciones; cantidad acorde al piso y tirada de jefe estable.

### 10. Iluminación, antorchas y niebla ambiental

Hacer pronto una prueba visual de una habitación y un pasillo para validar escala y visibilidad. Completar la ambientación cuando la geometría y navegación sean estables.

Antorcha del jugador: ancla al avatar, llama, luz y parpadeo con recursos existentes. Antorchas de pared: soporte visible apagado, activador independiente y grupo de luz/llama que se enciende al acercarse. No encender a través de paredes; comprobar proximidad y obstrucción. No desactivar el controlador de detección junto con la llama.

Revisar `DungeonRunFlameLight`, iluminación ambiental y shaders existentes. La revisión previa encontró efectos de luz pintada sin oclusión y un límite de ocho focos de ese efecto; contrastarlo con el código actual. No confundir esos efectos con luces reales que proyectan sombras. Priorizar luces relevantes y medir coste al aumentar salas y antorchas.

Revisar `WorldStyleGate`, cámara, perfil y Renderer de URP de la nueva escena. Hubo warnings de Renderer index 1 en una configuración Mobile con un único Renderer: comprobar referencias de calidad/plataforma sin perder el estilo por cambiar índices a ciegas.

Niebla: reutilizar partículas con baja opacidad y densidad ambiental, desligadas del descubrimiento. Limitar emisión a zonas relevantes si procede. Evitar cruces visibles a través de paredes cuando perjudiquen el resultado. No ocultar contenido ni usar superficies negras para simular habitaciones desconocidas.

Conservar la legibilidad del suelo, puertas, enemigos y E. Comparar con encuadre reproducible: base, iluminación estilizada, pixelado y combinación final cuando sea pertinente.

**Aceptación:** recorrido visible con antorcha, encendido coherente de paredes, atmósfera oscura sin negro plano dominante y capturas reales inspeccionadas.

### 11. Pruebas y cierre por hitos

Proporcionar al usuario pruebas C# cuando correspondan, manteniendo la restricción de no editarlas directamente. Priorizar propiedades y regresiones, no pruebas que repitan la implementación.

Validaciones de datos sobre un conjunto reproducible de semillas:

- Mismos parámetros/semilla producen los mismos datos relevantes.
- Habitaciones sin solapamiento y conexiones físicas coherentes.
- Todas las áreas necesarias alcanzables con el estado de puertas adecuado.
- Llave accesible antes del desbloqueo; escaleras bloqueadas antes y accesibles después.
- Decoración conserva accesos e interacciones obligatorias.
- Tipos, cantidades y posiciones especiales cumplen restricciones.
- Semillas de decoración no alteran geometría ni resultado de progresión.
- Configuraciones imposibles fallan de forma controlada, sin bucles infinitos.
- Interacciones únicas y transición resistente a doble activación.

Pruebas en Unity: compilación, consola, navegación, colisiones, E, retirada de obstáculos, puertas, varias transiciones, antorchas y niebla. Revisar rendimiento con un piso pequeño y otro cercano al máximo configurado. No fijar presupuestos arbitrarios: consultar los del proyecto.

La validación visual exige ejecutar/renderizar, capturar, inspeccionar, corregir y repetir. No afirmar validación de apariencia a partir de compilación o inspección de parámetros.

## Entregas y orden de trabajo

| Hito | Incluye | Evidencia necesaria |
| --- | --- | --- |
| A | Datos, presets, BSP, conexiones y representación básica | Piso conectado y reproducible, capturas de geometría. |
| B | Navegación sobre habitaciones y pasillos | Recorrido sin colisiones indebidas, salas vacías recorridas directamente. |
| C | Tipos, llave, puerta y escaleras de prueba | Bucle de acceso correcto antes/después de recoger llave. |
| D | Decoración, contenido e interacciones individuales | Conectividad validada, varios objetos sin interferencias. |
| E | Cambio de piso y progresión | Descensos sucesivos y tirada persistida en estado. |
| F | Ambientación y validación conjunta | Capturas, consola y rendimiento comprobados. |

Las dependencias lógicas de los puntos 6 y 7 deben respetarse aunque la entrega de contenido se distribuya entre hitos C y D. La prueba temprana de iluminación puede realizarse con un preset aislado sin adelantar todo el hito F.

En cada bloque entregar: objetivo, explicación, archivos afectados, código exacto para aplicar, configuración de Inspector, comprobación esperada y errores habituales. Al terminar informar qué se ha aplicado realmente, qué queda solo propuesto, estado de Console, pruebas/capturas realizadas y limitaciones.

## Decisiones pendientes y valores provisionales

- Navegación: mantener clic como supuesto de continuidad; preguntar antes de desarrollar una alternativa de teclado.
- Escala, anchura y número de salas: calibrar con el avatar y recursos reales; no tratar ejemplos numéricos como valores aprobados.
- Cantidades de cofres, curaciones, mejoras y eventos: configurables, con prioridades en pisos pequeños.
- Llave: inicialmente objeto accesible; recompensa de combate solo si se decide después.
- Probabilidad y estructura de jefe: resolver antes del cierre del hito E, sin detener las fases de geometría.
- Persistencia entre visitas o guardado en disco: fuera del alcance inicial salvo integración necesaria con sistemas existentes.
- WFC en asignación de tipos: extensión futura expresamente prevista en el punto 6, fuera de los hitos iniciales.

## Primera acción esperada del siguiente agente

Inspecciona el estado actual del proyecto y las clases de TFG pertinentes. Resume qué sigue siendo reutilizable y cualquier diferencia material respecto a este documento. Después prepara el primer bloque de contratos de datos y generación mínima para que yo lo aplique, con dependencias resueltas y una comprobación concreta. No reescribas de nuevo todo este plan ni modifiques C# directamente. Trabaja por entregas pequeñas que permitan aprender y detectar fallos antes de añadir la siguiente capa.
