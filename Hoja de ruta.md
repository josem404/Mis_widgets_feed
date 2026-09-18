# Hoja de ruta de Mis Widgets Feed

> Estado y propuesta del 18 de septiembre de 2026.

## Punto de partida confirmado

La primera rebanada vertical ya funciona en el Widgets Board real: el paquete está
registrado, **Mis Feed** aparece entre los feeds disponibles, la shell se carga desde
GitHub Pages y el puente `web → provider → web` responde correctamente. La observación
inicial fue de 243 ms para un `diagnostics.ping` completo.

El proyecto todavía no contiene un dashboard diseñado, notas, persistencia ni integración
con `Proyectos_widgets_WSL`. GitHub Pages es por ahora la referencia funcional, no una
decisión arquitectónica definitiva.

## Principio de decisión

Mi recomendación es resolver primero las incertidumbres de plataforma que pueden obligar
a rehacer toda la solución. El orden debe ser: **entrega local, capacidades reales del
host, contrato de datos, primera nota y solo después diseño del tablón**.

El objetivo no es eliminar GitHub Pages a cualquier precio. Es determinar si Windows
permite una solución local, empaquetada y bajo demanda que siga siendo más simple y
robusta que la alternativa remota. Cada fase termina con una decisión explícita.

## Fase 0 — Cerrar la referencia funcional

Objetivo: conservar una base fiable contra la que comparar experimentos.

- Repetir conexión tras cerrar y volver a abrir el Board.
- Deshabilitar y habilitar **Mis Feed** y revisar el log de ciclo de vida.
- Medir carga con red normal, red lenta y red desconectada.
- Confirmar que el provider termina cuando deja de ser necesario.
- Guardar una pequeña tabla de resultados, versión de Windows y versión del paquete.

Criterio de salida: la variante GitHub Pages tiene un comportamiento reproducible y una
secuencia de recuperación conocida.

## Fase 1 — Sonda de shell completamente local

Esta es la siguiente fase recomendada y la de mayor valor arquitectónico.

### Experimento A: URI de paquete

Empaquetar una shell mínima y probar `ms-appx` y `ms-appx-web` como `ContentUri`. No están
documentados para feeds y probablemente el host los rechace, pero la prueba es barata y
acota rápidamente el comportamiento.

### Experimento B: origen HTTPS lógico y recursos interceptados

Usar, por ejemplo, `https://feed.miswidgets.invalid/index.html` como origen lógico,
declarar un `WebRequestFilter` limitado a ese host e implementar
`IFeedResourceProvider`. El provider devolvería exclusivamente archivos incluidos en una
lista generada durante el build:

- `index.html` y assets Vite con tipos MIME correctos;
- estados 200/404 explícitos y cabeceras CSP/cache controladas;
- solo `GET`, sin rutas relativas ambiguas ni acceso general al sistema de archivos;
- límites de tamaño, normalización de ruta y logs sin contenido personal.

La pregunta decisiva es si la navegación inicial de `ContentUri` llega a
`OnResourceRequested`. Si llega, podemos tener una shell web completamente empaquetada,
sin servidor local ni Internet. Si solo llegan recursos secundarios, la shell inicial
seguirá necesitando un origen remoto.

### Experimento C: localhost, solo como último recurso

Un listener local añade selección y colisión de puertos, arranque antes de la navegación,
posibles restricciones de loopback, superficie HTTP, coordinación con el proceso COM y
riesgo de mantenerlo residente. Solo merece una prueba si A y B fallan y si el host admite
claramente el origen local. No instalaría un servicio ni reservaría un puerto permanente.

### Cómo aislar la prueba

Mantendría el feed actual como control y añadiría temporalmente una segunda `Definition`
local dentro del mismo paquete. Así pueden compararse ambas variantes en el mismo host y
la sonda se retira con una actualización normal del manifiesto, sin crear otra identidad
MSIX ni depender de desinstalaciones destructivas.

Criterio de salida: una tabla que clasifique cada ruta como funcional, parcialmente
funcional o descartada, incluyendo arranque sin red, reapertura, puente, assets, caché,
logs, memoria y terminación.

## Fase 2 — Laboratorio de capacidades del host web

Antes de diseñar UI, crear una página diagnóstica temporal que pruebe únicamente:

- dimensiones, resize, zoom y densidad;
- tema claro/oscuro y contraste;
- teclado, foco, Narrador y touch;
- enlaces externos, navegación, descargas y ventanas emergentes;
- clipboard y drag-and-drop;
- almacenamiento web disponible, aunque no se use como fuente de verdad;
- CSP, `fetch`, carga de imágenes y comportamiento de caché;
- suspensión/reanudación y pérdida/reconexión del provider.

No se trata de incorporar todas estas capacidades, sino de saber cuáles son fiables dentro
del iframe del Board. El resultado será una matriz de compatibilidad propia.

## Fase 3 — Modelo local y contrato v2

Diseñar antes de programar el modelo mínimo de un elemento del tablón:

- identidad estable, tipo, texto, enlace y fechas;
- posición/orden como dato separado de la presentación;
- revisión para detectar escrituras obsoletas;
- límites de longitud y tamaño;
- esquema versionado y migraciones;
- comandos concretos: listar, crear, actualizar y eliminar.

La fuente de verdad debe ser nativa y local. La web presenta y edita; el provider valida y
persiste. No expondría rutas arbitrarias, SQL, comandos del sistema ni un API genérico.
Primero compararía JSON con escritura atómica frente a SQLite; para unas pocas notas JSON
puede bastar, mientras que SQLite cobra sentido con búsquedas, adjuntos y muchas entidades.

Criterio de salida: contrato documentado, amenazas identificadas y pruebas puras del
protocolo y las migraciones, todavía sin diseñar el dashboard final.

## Fase 4 — Primera rebanada de producto

Implementar solo notas de texto plano:

1. cargar una lista local bajo demanda;
2. crear una nota;
3. editarla con control de revisión;
4. eliminarla con confirmación y posibilidad de recuperación;
5. verificar reapertura del Board y funcionamiento sin red.

Esta fase debe medir latencia, memoria y activaciones. No habrá polling: la web solicita el
estado al mostrarse y el provider responde bajo demanda.

## Fase 5 — Diseño del tablón

Con la infraestructura probada, decidir navegación, columnas, densidad, búsqueda,
selección, edición y estados vacíos/error. Conviene diseñar con datos reales de la fase 4,
no con tarjetas ficticias. La accesibilidad y el responsive serán criterios de aceptación,
no tareas posteriores.

## Fase 6 — Tipos adicionales

Añadir de uno en uno, con contratos específicos:

- enlaces web con título, favicon y apertura externa;
- imágenes locales mediante recursos intermediados y límites estrictos;
- archivos y carpetas como referencias, no como copias implícitas;
- contenido enriquecido solo después de definir saneado y formato portable.

No intentaría incrustar webs externas completas: muchas impiden el framing y aumenta mucho
la superficie de seguridad. Es preferible guardar una tarjeta local con metadatos y abrir
la página en el navegador.

## Fase 7 — Interoperabilidad con Mis Widgets

Solo cuando el feed sea estable se definirá un contrato explícito entre repositorios. Las
opciones a evaluar son archivos de intercambio versionados, activación por protocolo o un
broker compartido bajo demanda. No se compartirán bases de datos internas, assemblies ni
procesos residentes. Cada proyecto conservará identidad, estado, despliegue y fallos
independientes.

## Fase 8 — Endurecimiento y distribución

- Firma y estrategia MSIX fuera del modo desarrollador.
- Actualización del paquete y compatibilidad de datos.
- Política offline y recuperación de errores.
- Telemetría solo si se decide expresamente y nunca para contenido personal.
- Validación en más builds, cuentas, escalas y configuraciones EEE.
- Revisión de la evolución de la API, que continúa en preview.

## Decisiones que no tomaría todavía

- Framework web: TypeScript sin framework basta para las sondas y la primera nota.
- Diseño visual definitivo: depende de la geometría y accesibilidad reales del host.
- SQLite por anticipación: primero medir el modelo y volumen reales.
- Múltiples feeds: un único tablón reduce estados y mantenimiento hasta conocer el uso.
- `localhost` como arquitectura: es más complejo que recursos interceptados y contradice
  el objetivo de un provider ligero si obliga a mantener un listener.

## Próximo hito propuesto

El siguiente hito será **Local Shell Probe**: conservar la shell de Pages, añadir una
definición local temporal e implementar las sondas A y B con diagnóstico suficiente para
responder, en una sola sesión de pruebas nativas, si el Board puede cargar toda la
experiencia web desde el MSIX sin conexión a Internet.
