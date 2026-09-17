# Investigación: paneles y dashboards del Windows Widgets Board

> Investigación documental realizada el 2026-09-18.
>
> Esta sesión es exclusivamente de investigación. No se modifica la implementación
> existente de Mis Widgets. Las conclusiones distinguen entre lo que Microsoft documenta,
> lo que se deduce razonablemente de esa documentación y lo que todavía requiere una
> prueba nativa en un Windows elegible.

## 1. Conclusión ejecutiva

La hipótesis inicial era esencialmente correcta, pero hay que separar dos conceptos que
Microsoft llama de forma parecida:

1. **Los dashboards del propio Widgets Board**: son las superficies que Windows muestra en
   la barra de navegación lateral, por ejemplo el dashboard de widgets del usuario y el
   dashboard integrado Discover/My feed. Su navegación, orden y existencia pertenecen al
   host de Windows.
2. **Los feed providers de terceros**: son el punto de extensibilidad público que permite a
   una aplicación añadir contenido web al área de feeds del Board. Microsoft los describe
   como una integración para registrar uno o varios feeds, mostrados como pivots sobre la
   sección de feeds.

La API pública documentada **no ofrece una forma de registrar una nueva entrada arbitraria
de primer nivel en la barra lateral, crear un dashboard equivalente a “My Widgets”, ni
reemplazar Discover**. La vía más cercana a un “panel completo” es un `FeedProvider`: en
la práctica puede alojar una experiencia HTML/JavaScript grande dentro del Board, pero
el resultado sigue siendo un feed/pivot administrado por el host, no un dashboard de
Windows de propiedad de nuestra app.

La restricción geográfica también queda confirmada:

- **Feed providers**: feature en preview y disponible solo para usuarios del Espacio
  Económico Europeo (EEE/EEA).
- **Web widget providers**: widgets individuales cuyo cuerpo se sirve como HTML remoto;
  también están limitados al EEE.
- **Widget providers normales** basados en Adaptive Cards y los widgets PWA documentados
  no tienen esa restricción geográfica en la documentación consultada.

Por tanto, un panel HTML integrado en el Board sería una línea de investigación válida
para una instalación en España/EEE, pero no debe convertirse en una dependencia del
producto general sin una estrategia alternativa para el resto de regiones.

## 2. Qué significa “panel” en Windows Widgets

### 2.1 La superficie que controla Windows

El Widgets Board es el host de Windows. En las versiones recientes que Microsoft ha ido
probando, la barra lateral permite cambiar entre un dashboard dedicado de widgets y otras
vistas integradas como Discover o My feed. Microsoft presentó esta experiencia en los
Insider builds 26058/26090 y volvió a describirla como “Multiple dashboards” durante los
despliegues de 2025.

Esto describe una capacidad del producto Windows, no un contrato de extensibilidad para
que una app añada sus propios botones a esa barra. En la documentación pública revisada,
los elementos que una aplicación puede registrar son widgets o feeds, no dashboards de
navegación.

### 2.2 El punto de extensibilidad de terceros

Un `FeedProvider` se registra mediante la extensión de paquete:

```xml
<uap3:Extension Category="windows.appExtension">
  <uap3:AppExtension
      Name="com.microsoft.windows.widgets.feeds"
      DisplayName="Mis Widgets"
      Id="MisWidgetsFeeds"
      PublicFolder="Public">
    <uap3:Properties>
      <FeedProvider
          Description="Feeds de Mis Widgets"
          Icon="ms-appx:Images\StoreLogo.png">
        <Activation>
          <CreateInstance ClassId="00000000-0000-0000-0000-000000000000" />
        </Activation>
        <Definitions>
          <Definition
              Id="MisWidgets_Launcher_Feed"
              DisplayName="Launcher"
              Description="Accesos y estado del Launcher"
              ContentUri="https://ejemplo.invalid/miswidgets/launcher"
              Icon="ms-appx:Images\LauncherFeed.png" />
        </Definitions>
      </FeedProvider>
    </uap3:Properties>
  </uap3:AppExtension>
</uap3:Extension>
```

El XML anterior es un esquema ilustrativo, no una modificación propuesta para el
proyecto: el CLSID, las rutas de assets, el host web y el modelo de seguridad todavía no
están decididos.

La documentación del manifiesto define estos niveles:

| Nivel | Propósito |
|---|---|
| `FeedProvider` | Identidad, icono y descripción del proveedor. |
| `Activation/CreateInstance` | CLSID del servidor COM fuera de proceso que implementa `IFeedProvider`. |
| `Definitions/Definition` | Un feed individual con `Id`, nombre, descripción, `ContentUri` e icono. |
| `WebRequestFilter` | Opcionalmente intercepta determinadas peticiones web para que las atienda `IFeedResourceProvider`. |
| `ExcludedRegions` / `ExclusiveRegions` | Disponibilidad geográfica declarada para un feed concreto. No elimina la restricción general del host EEA. |

El proveedor puede declarar **varios `Definition`**. Microsoft confirma que una aplicación
puede admitir un feed o múltiples feeds; en el Board aparecen como feeds/pivots
independientes. Eso permite presentar varias experiencias especializadas, aunque no
permite afirmar que cada una se convierta en un dashboard de primer nivel.

## 3. Estado geográfico y evolución de la función

### 3.1 Por qué aparece el EEE

En noviembre de 2023 Microsoft anunció nuevos puntos de interoperabilidad en Windows para
cumplir el Digital Markets Act. Entre ellos estaba explícitamente “Feeds in the Windows
Widgets Board”, disponible en el EEE. Microsoft también documentó que la región usada para
estas obligaciones se basa en la región elegida durante la configuración del dispositivo.

La documentación actual de **Dashboards and feed providers**, actualizada el 2026-06-25,
sigue indicando simultáneamente que:

- la característica está en preview;
- solo está disponible para usuarios del EEE;
- requiere el Windows App SDK más reciente;
- hay que cumplir directrices técnicas y de diseño específicas.

No hay base documental para tratar el feed provider como una API mundial ya estabilizada.

### 3.2 No confundirlo con “Multiple dashboards”

Los blogs de Windows Insider describen una barra lateral y un dashboard dedicado de
widgets. La experiencia fue inicialmente probada en canales Insider y vinculada al
desarrollo del Board; posteriormente Microsoft anunció nuevos despliegues de “Multiple
dashboards” y otras vistas integradas.

La lectura correcta es:

- **Windows puede ofrecer varios dashboards propios**, según la versión, canal, despliegue
  gradual y configuración del dispositivo.
- **Una app puede registrar feeds**, según el contrato `com.microsoft.windows.widgets.feeds`
  y la elegibilidad EEA.
- Lo segundo no demuestra que una app pueda crear un dashboard lateral nuevo. La
  documentación pública consultada no expone un API para ello.

## 4. Qué puede contener el feed

### 4.1 Formato documentado

El contenido de un feed se desarrolla como un **componente web** y Windows lo renderiza en
un **iframe** dentro del host de feeds. A diferencia de un widget normal, la experiencia no
se limita al árbol declarativo de una Adaptive Card para su contenido visual.

El formato práctico sería una aplicación web responsive:

- HTML semántico y componentes propios.
- CSS para composición, jerarquía visual, tema claro/oscuro y estados de carga.
- JavaScript para navegación interna, filtros, formularios, selección y actualización de
  la vista.
- Imágenes, iconografía, tablas, listas, tarjetas, gráficos y otras visualizaciones web.
- Enlaces y acciones que abran contenido más profundo fuera del Board cuando la tarea ya
  no sea apropiada para una superficie de vistazo.

La lista anterior es el potencial normal de una experiencia HTML en un iframe, no una
promesa de que cada API web o cada control se comporte igual dentro del host de Windows.
El contrato de Microsoft especifica el iframe, la URL y los mecanismos de comunicación,
pero no publica una tabla exhaustiva de todos los elementos HTML permitidos ni un sistema
de layout propio para “dashboards”. La compatibilidad real debe probarse en el Widgets
Board de una build elegible.

### 4.2 Comunicación con el provider nativo

El namespace `Microsoft.Windows.Widgets.Feeds.Providers` expone una superficie más amplia
que el simple registro del feed:

| Capacidad | Mecanismo | Posible uso |
|---|---|---|
| Ciclo de vida | `IFeedProvider` | Saber cuándo se habilita/deshabilita el proveedor o un feed. |
| Parámetros dinámicos | `FeedManager.SetCustomQueryParameters` | Añadir parámetros de sesión o autenticación a `ContentUri`; regenerarlos si el host los solicita. |
| Web → provider | `IFeedProviderMessage.OnMessageReceived` | Enviar una orden o selección desde JavaScript al proceso nativo. |
| Provider → web | `FeedManager.SendMessageToContent` | Enviar estado o resultados al contenido web. |
| Recursos | `IFeedResourceProvider.OnResourceRequested` | Servir o modificar respuestas de recursos que coincidan con `WebRequestFilter`; útil para interponer autenticación o recursos locales. |
| Diagnóstico | `IFeedProviderAnalytics`, `IFeedProviderErrors` | Recibir callbacks opcionales de interacción y errores del host. |
| Avisos | `FeedManager.TryShowAnnouncement` y `FeedAnnouncement` | Solicitar un aviso asociado al feed y, según las políticas del host, mostrar badge/aviso en la barra de tareas. |

Los mensajes documentados son cadenas; el propio contenido puede serializar JSON, pero el
contrato no convierte automáticamente el HTML en controles WinUI ni concede a la página
acceso directo al sistema de archivos, a procesos o a las APIs nativas. Las operaciones
privilegiadas tendrían que pasar por el provider y por un protocolo explícito y validado.

### 4.3 Autenticación y datos locales

El diseño oficial espera que el provider genere parámetros de consulta cuando el feed se
habilita y que los regenere si `OnCustomQueryParametersRequested` se dispara, por ejemplo
tras un fallo al obtener contenido remoto. Esto permite tokens o parámetros temporales,
pero también implica tratar la URL y los logs con cuidado: no se deben exponer secretos en
la interfaz ni persistir tokens sin necesidad.

Para datos locales de Mis Widgets hay dos posibilidades conceptuales:

1. **Provider como broker**: el HTML envía un mensaje, el proceso nativo consulta el
   catálogo/estado local y devuelve un resultado serializado.
2. **Recursos intermediados**: el provider atiende peticiones concretas mediante
   `IFeedResourceProvider`.

La primera opción es más explícita y fácil de auditar. Ninguna de las dos debe entenderse
como permiso para mantener un servicio residente, observar continuamente el sistema o
actualizar el Board espontáneamente. La política del proyecto de mantener el Board ligero
y el trabajo bajo demanda sigue siendo aplicable.

## 5. Cómo se implementaría, si se decide hacer una prueba

La ruta documentada para un feed provider Win32 es la siguiente:

1. **Plataforma de prueba**: Windows 11 23H2, build 22631.2787 o posterior, Developer Mode
   para desarrollo, Visual Studio 2026 o posterior y Windows App SDK 2.3.1 o posterior
   según el walkthrough actual.
2. **Aplicación empaquetada**: la documentación dice que actualmente solo las apps
   empaquetadas pueden registrarse como feed providers. El paquete debe declarar un
   `ComServer` fuera de proceso y la extensión `uap3:AppExtension` de feeds.
3. **Servidor COM**: implementar `IFeedProvider`, asignar un CLSID permanente, crear una
   class factory y registrar el objeto con `CoRegisterClassObject` cuando el host active el
   proceso.
4. **Manifest**: declarar la identidad del provider y uno o varios feeds con su
   `ContentUri`, iconos y textos localizables.
5. **Experiencia web**: publicar el endpoint web y diseñarlo para el ancho/alto disponible,
   cambios de tema, carga lenta, error de red y navegación por teclado/lectores de pantalla.
6. **Callbacks**: responder a habilitación/deshabilitación y a las peticiones de renovación
   de parámetros; implementar solo las interfaces opcionales que tengan una necesidad real.
7. **Prueba nativa**: instalar/desplegar el paquete en un dispositivo EEA elegible, abrir el
   Board, habilitar el feed, verificar el pivot, comprobar mensajes y recursos, y medir
   comportamiento al cerrar/abrir el Board y al deshabilitar el feed.
8. **Distribución**: para publicación, Microsoft documenta la Microsoft Store y permite
   solicitar la inclusión en la colección “Feeds Store Collection”. La instalación desde
   GitHub seguiría siendo una distribución MSIX con sus requisitos de firma/sideloading.

La última versión estable general del Windows App SDK consultada es 2.4.0, publicada el
2026-08-13. El walkthrough de feed provider mantiene “2.3.1 o posterior” como requisito
mínimo de ejemplo. Esto no autoriza a actualizar la dependencia del proyecto durante esta
sesión; cualquier actualización futura necesita su propia validación de compilación,
despliegue y comportamiento del Board.

## 6. Comparativa de las rutas disponibles

| Ruta | Superficie | Formato visual | Región documentada | ¿Crea dashboard lateral propio? |
|---|---|---|---|---|
| Widget provider normal | Tarjeta en el dashboard de widgets | Adaptive Card JSON + data binding | Sin restricción EEA documentada | No |
| Widget provider web | Un widget individual | HTML remoto mediante `metadata.webUrl`, con fallback Adaptive Card | Solo EEA | No |
| Feed provider | Feed/pivot en la zona de feeds | Componente web HTML dentro de iframe | Solo EEA; preview | No está documentado |
| PWA-driven widget | Widget individual | Adaptive Card declarativa servida por PWA/service worker | Sin restricción EEA documentada | No |
| Dashboard del Board | Superficie de navegación del host | Controlado por Windows y sus servicios | Depende de Windows y del despliegue | No es una extensión pública de terceros |

La diferencia decisiva entre el widget web y el feed provider es la **ubicación y la
semántica de la experiencia**, no solo el hecho de utilizar HTML. El widget web sigue
siendo una tarjeta individual; el feed provider se integra en el sistema de feeds y puede
tener una experiencia web mucho más parecida a una página/panel.

## 7. Diseños posibles para Mis Widgets

### Opción A: un feed “Mis Widgets” con navegación interna

Un único `Definition` podría mostrar una experiencia coherente con varias secciones:

- Inicio/resumen del día.
- Launcher de aplicaciones, carpetas, archivos y atajos.
- Notas y elementos pendientes.
- Vistas de Obsidian o de otras fuentes.
- Estado del sistema y acciones rápidas no destructivas.

Ventaja: una sola identidad, una sola política de seguridad y navegación interna controlada
por nuestra aplicación. Inconveniente: el Board solo lo vería como un pivot; Windows no
conocería cada sección como un dashboard separado.

### Opción B: varios feeds especializados

El mismo provider podría declarar pivots como “Launcher”, “Notas”, “Obsidian” y
“Diagnóstico”. Cada uno tendría su `ContentUri`, icono y estado de habilitación.

Ventaja: acceso directo y separación clara. Inconvenientes: más superficie de mantenimiento,
más endpoints y más estados de activación; además, el usuario puede deshabilitar feeds
individualmente y la disponibilidad general sigue dependiendo del EEE.

### Opción C: widget normal como fallback mundial

Mantener el Launcher y el resto de widgets Adaptive Card como experiencia universal, y
reservar el feed provider para una edición o capacidad adicional EEA. Es la estrategia con
menor riesgo de producto porque no hace depender la funcionalidad básica de una feature en
preview.

### Opción D: app acompañante como dashboard completo real

Si el objetivo principal es controlar totalmente navegación, ventanas, almacenamiento,
acceso a datos locales y compatibilidad mundial, la app WinUI acompañante sigue siendo la
superficie adecuada. Puede reutilizar el modelo semántico y el diseño del proyecto sin
pretender que el Widgets Board sea una aplicación web general.

## 8. Riesgos y preguntas abiertas

1. **Preview y elegibilidad**: la documentación no promete estabilidad ni disponibilidad
   fuera del EEE. Hay que probar la versión exacta de Windows, Web Experience y Windows App
   SDK del dispositivo.
2. **No hay API de dashboard lateral**: no conviene diseñar un contrato interno suponiendo
   que un feed pivot podrá convertirse después en un botón de primer nivel sin cambios.
3. **Contenido remoto**: la experiencia depende de DNS, TLS, disponibilidad del servidor,
   políticas del WebView y tiempos de carga. Debe tener estados offline/error y una ruta de
   recuperación.
4. **Personalización**: la página general de feeds menciona controles opcionales de
   personalización, pero el formato público del manifest consultado no expone un elemento
   detallado equivalente al sistema de personalización de `IWidgetProvider2`. Hay que
   verificar este punto con una muestra real o documentación adicional antes de prometer
   una personalización nativa del feed.
5. **Seguridad del puente**: los mensajes web→provider deben usar comandos tipados,
   validación de origen/contexto, límites de tamaño y confirmaciones para acciones
   disruptivas. No se debe convertir un mensaje de la página en una orden nativa genérica.
6. **Accesibilidad y entrada**: el iframe no hereda automáticamente la semántica de los
   widgets Adaptive Card. Teclado, narrador, foco, contraste, zoom y touch deben diseñarse
   y probarse en la página web.
7. **Rendimiento**: un panel HTML puede hacer mucho más que una tarjeta, pero esa libertad
   puede producir más CPU, memoria y red. La página debe ser ligera, sin polling agresivo ni
   actividad cuando el feed no está visible.
8. **Persistencia/lifecycle**: el provider puede ser activado por el host bajo demanda y
   recibir callbacks de deshabilitación. El estado que deba sobrevivir debe persistirse en
   la aplicación, no en referencias a objetos de callback cuyo tiempo de vida termina al
   finalizar la llamada.

## 9. Recomendación de investigación para el proyecto

No implementar todavía. La siguiente fase, si se retoma esta línea, debería ser una prueba
aislada y desechable, no una ampliación inmediata del provider de producción:

1. Confirmar en un Windows configurado en España/EEE que el sistema muestra la experiencia
   de feeds y que una app de prueba puede aparecer en el Board.
2. Crear el mínimo feed HTML estático con un solo `Definition` y sin acceso a datos locales.
3. Verificar el ciclo COM, el pivot, el cierre/reapertura del Board y la deshabilitación del
   feed.
4. Añadir un único mensaje tipado web→provider y una respuesta provider→web.
5. Medir carga, memoria, navegación, accesibilidad y comportamiento con red desconectada.
6. Solo después decidir entre un feed único, varios feeds o mantener toda la experiencia en
   la app acompañante.

La decisión provisional recomendada es: **mantener los widgets Adaptive Card actuales como
producto principal; documentar el feed provider como una capacidad experimental EEA para
una futura investigación; no llamarlo “dashboard propio” hasta que Microsoft publique un
contrato de navegación de primer nivel o una prueba nativa demuestre algo distinto.**

## 10. Fuentes consultadas

Fuentes oficiales de Microsoft y Microsoft Open Source, consultadas el 2026-09-18:

- [Dashboards and feed providers](https://learn.microsoft.com/en-us/windows/apps/develop/feeds/feed-providers) — descripción, pivots, preview y limitación EEA.
- [Implement a feed provider in a C# Windows App](https://learn.microsoft.com/en-us/windows/apps/develop/feeds/implement-feed-provider-cs) — requisitos, `IFeedProvider`, COM, MSIX, pruebas y publicación.
- [Feed provider package manifest XML format](https://learn.microsoft.com/en-us/windows/apps/develop/feeds/feed-provider-manifest) — extensión `com.microsoft.windows.widgets.feeds`, definitions, URI, iconos y regiones.
- [Microsoft.Windows.Widgets.Feeds.Providers namespace](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.widgets.feeds.providers?view=windows-app-sdk-1.8) — interfaces y clases públicas del provider.
- [FeedManager class](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.widgets.feeds.providers.feedmanager?view=windows-app-sdk-1.8) — parámetros, mensajes y anuncios.
- [Web widget providers](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/web-widget-providers) — HTML remoto en widgets individuales, recursos, mensajes y limitación EEA.
- [Widget provider package manifest XML format](https://learn.microsoft.com/en-us/windows/apps/develop/widgets/widget-provider-manifest) — contraste con el registro de widgets normales.
- [Windows Widgets overview](https://learn.microsoft.com/en-us/windows/apps/design/widgets/) — terminología del host, Board y widgets.
- [Windows Insider: Build 26058](https://blogs.windows.com/windows-insider/2024/02/14/announcing-windows-11-insider-preview-build-26058/) y [Build 26090](https://blogs.windows.com/windows-insider/2024/03/28/announcing-windows-11-insider-preview-build-26090-canary-and-dev-channels/) — barra lateral, dashboard de widgets y Discover en Insider.
- [Windows Insider: Build 26120.4161](https://blogs.windows.com/windows-insider/2025/05/23/announcing-windows-11-insider-preview-build-26120-4161-beta-channel/) y [Windows Experience Blog, 2025-10-16](https://blogs.windows.com/windowsexperience/2025/10/16/new-experiences-currently-rolling-out-for-windows-11/) — despliegues posteriores de múltiples dashboards.
- [DMA changes in the EEA](https://blogs.windows.com/windows-insider/2023/11/16/previewing-changes-in-windows-to-comply-with-the-digital-markets-act-in-the-european-economic-area/) — origen regulatorio y alcance regional de los feeds.
- [Windows App SDK release notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0) — versión estable 2.4.0 consultada y fechas de lanzamiento.
- [Windows App SDK samples](https://github.com/microsoft/WindowsAppSDK-Samples) — repositorio oficial de muestras; no se ha incorporado código al proyecto durante esta investigación.

## 11. Aclaraciones posteriores: pestaña, cajón de notas y contenido local

### 11.1 ¿Dónde aparecería un feed provider?

El resultado esperado tiene dos niveles:

```text
Widgets Board
├── My Widgets / dashboard de widgets       <- superficie del host
└── Discover / My feed / zona de feeds      <- superficie del host
    ├── feed de Microsoft
    ├── Mis Widgets: Launcher               <- pivot/feed de terceros
    └── Mis Widgets: Notas                  <- otro pivot/feed, si se declara
```

Por tanto, **cada `Definition` puede tener su propia pestaña o pivot**, con su nombre e
icono, pero esa pestaña vive dentro de la experiencia de feeds. No se registra como un
nuevo dashboard lateral de primer nivel. El texto exacto de la navegación puede variar
entre builds y despliegues (`Discover`, `My feed` o una denominación equivalente), pero la
frontera técnica es la misma: Windows controla la navegación superior y la app controla el
contenido de su feed.

### 11.2 El “provider como broker”, explicado como flujo

El broker no significa que el HTML pueda llamar directamente a C# o abrir un archivo. El
flujo sería explícito:

```text
Usuario pulsa en el tablón
        ↓
JavaScript del feed: postMessage(JSON con comando y requestId)
        ↓
Widgets host → IFeedProviderMessage.OnMessageReceived
        ↓
Provider nativo valida el comando y lee/escribe el almacenamiento local
        ↓
FeedManager.SendMessageToContent(JSON de respuesta)
        ↓
JavaScript actualiza el DOM del tablón
```

Ejemplo conceptual de mensaje, no contrato definitivo:

```json
{
  "type": "load-drawer",
  "requestId": "r-184",
  "collectionId": "personal"
}
```

El provider podría leer las bibliotecas, notas y preferencias persistidas por Mis Widgets,
devolver un listado serializado y dejar que el HTML lo pinte. Para guardar una nota, el
flujo sería el mismo en sentido inverso: el HTML envía una orden tipada, el provider valida
los campos, persiste y responde con el resultado. No hay una respuesta automática del
host: el `requestId` y el formato de respuesta serían responsabilidad de nuestra
arquitectura.

La ventaja es que la frontera de seguridad queda visible: el HTML solo puede pedir las
operaciones que el provider decida exponer. La desventaja es que hay que diseñar un pequeño
protocolo, gestionar errores y asumir que el proceso nativo puede activarse y desactivarse
según el ciclo de vida que administre el Board.

### 11.3 Recursos intermediados, explicado con un ejemplo

En la segunda opción el HTML pide recursos como si fueran URLs web:

```text
HTML solicita https://miswidgets.invalid/local/items.json
        ↓
La URL coincide con WebRequestFilter
        ↓
IFeedResourceProvider.OnResourceRequested
        ↓
Provider lee el recurso local y devuelve cuerpo, tipo MIME y estado HTTP
        ↓
El HTML recibe la respuesta y la usa con fetch()/img/etc.
```

Esto encaja bien con imágenes o respuestas JSON pequeñas. También permite que el provider
añada cabeceras o tokens y que el host siga recuperando por Internet las URLs para las que
el provider no produce una respuesta.

No es equivalente a convertir el feed entero en una aplicación nativa: la página sigue
siendo web y el filtro solo cubre las peticiones que coincidan. En particular, la
documentación pública no presenta `IFeedResourceProvider` como un mecanismo para sustituir
el `ContentUri` inicial por una carpeta local completa.

### 11.4 Encaje con la idea del cajón o tablón

La idea es técnicamente coherente con un feed HTML único. Ese feed podría comportarse como
un tablón personal con:

- notas adhesivas, textos enriquecidos y etiquetas;
- imágenes locales o remotas;
- enlaces a webs y favoritos;
- tarjetas de aplicaciones, carpetas, archivos y atajos;
- vistas previas, miniaturas y metadatos;
- columnas, grupos, filtros, ordenación y navegación interna;
- acciones para abrir el destino en la aplicación correspondiente.

El feed podría ofrecer drag-and-drop y edición dentro de su propio HTML si la página lo
implementa. Eso sería interacción de la página, no una capacidad nativa del Widgets Board
para ordenar objetos arbitrarios del tablón. Las webs externas tampoco garantizan que se
puedan incrustar: muchas impiden el framing mediante sus políticas de seguridad. En esos
casos el tablón puede mostrar título, imagen, resumen y enlace de apertura.

### 11.5 ¿Hace falta Internet?

Hay que distinguir tres respuestas:

1. **Feed provider documentado por Microsoft**: sí depende de un `ContentUri` y de una
   experiencia web cargada en un iframe. Los ejemplos oficiales usan `https://` y describen
   el feed como contenido remoto. La documentación consultada no garantiza que
   `ms-appx:`, una ruta de fichero o una página HTML incluida en el MSIX sean valores
   compatibles para el documento inicial del feed.
2. **Interfaz web con datos locales**: conceptualmente sí se puede separar interfaz y datos.
   La página puede ser una shell web y el provider actuar como broker local mediante
   mensajes o recursos intermediados. En ese modelo Internet podría limitarse a servir la
   shell, actualizaciones o imágenes remotas; los datos personales seguirían en local.
3. **Sin web de por medio**: no es el camino del feed provider. Para una superficie local
   pura habría que usar la app WinUI acompañante o widgets normales basados en Adaptive
   Cards. Estos últimos pueden mostrar texto, imágenes, enlaces y acciones con datos
   locales, pero no proporcionan una página libre tipo tablón ni permiten incrustar webs
   arbitrarias como un navegador.

Un servidor HTTP local (`localhost`) o una URL de paquete como hipótesis de laboratorio
podrían conservar el esquema web sin depender de Internet, pero no aparecen como ruta
soportada en la documentación del feed provider. Serían una prueba experimental con
incertidumbres adicionales de ciclo de vida, permisos, carga inicial y comportamiento del
host; no deben formar parte del diseño garantizado hasta validarlo en el Board real.

La consecuencia de diseño es clara: **si el requisito esencial es un cajón local y rico,
la app acompañante es la opción sólida; si se acepta un feed web EEA en preview, el feed
puede ser una excelente vista de tablón con datos locales detrás de un puente controlado.**
