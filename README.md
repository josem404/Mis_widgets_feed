# Mis Widgets Feed

Proyecto paralelo e independiente de `Proyectos_widgets_WSL` para explorar un panel web
personal dentro del área de feeds del Windows Widgets Board.

La primera implementación es una rebanada vertical deliberadamente pequeña:

- un único feed llamado **Mis Feed**;
- una shell TypeScript/Vite publicada en GitHub Pages;
- un provider C# empaquetado como MSIX y activado como servidor COM;
- un mensaje de diagnóstico `web → provider → web`;
- sin notas, persistencia, acciones nativas ni interoperabilidad entre repositorios.

La rebanada quedó validada en el Widgets Board real el 18 de septiembre de 2026:
**Mis Feed** aparece en la lista de feeds, puede habilitarse y la prueba completa
`web → provider → web` respondió correctamente en 243 ms. Esto demuestra la integración
actual; no elimina las limitaciones de preview y EEE de la plataforma.

La investigación de partida y las limitaciones de preview/EEE están en
[Investigación paneles y dashboards del Widgets Board.md](Investigación%20paneles%20y%20dashboards%20del%20Widgets%20Board.md).
Los siguientes experimentos y criterios de decisión están en
[Hoja de ruta.md](Hoja%20de%20ruta.md).
La separación operativa de los dos proyectos está en [AGENTS.md](AGENTS.md).

## Arquitectura

```text
GitHub Pages
  https://josem404.github.io/Mis_widgets_feed/
             │ window.chrome.webview.postMessage(JSON)
             ▼
Windows Widgets Board
             │ IFeedProviderMessage.OnMessageReceived
             ▼
MisWidgets.Feed.Provider.exe
             │ FeedManager.SendMessageToContent(JSON)
             └──────────────────────────────────────► shell web
```

| Componente | Responsabilidad |
|---|---|
| `web/` | Shell accesible, protocolo cliente y build estático. |
| `src/MisWidgets.Feed.Core/` | Contrato JSON y coordinación pura, sin dependencias de Windows. |
| `src/MisWidgets.Feed.Provider/` | Activación COM, callbacks de feeds, logs y self-test empaquetado. |
| `tests/MisWidgets.Feed.Tests/` | Pruebas del protocolo y del ciclo de vida ejecutables desde WSL. |
| `tools/` | Verificación de Pages y despliegue nativo conservador. |

Los tres iconos MSIX iniciales son copias estáticas y provisionales de la identidad visual
de Mis Widgets. No crean una dependencia de código ni de ejecución entre repositorios; se
reemplazarán cuando se diseñe una identidad específica para el feed.

## Protocolo v1

La web solo puede enviar `diagnostics.ping`. El provider valida versión, tipo, UUID,
payload, IDs registrados y un límite de 8 KiB antes de responder con
`diagnostics.pong` o `diagnostics.error`. No existe ningún comando genérico que pueda
convertirse en una operación del sistema.

## Desarrollo web

Requiere Node.js 24.

La preparación reproducible de la copia WSL instala Node 24 para Linux y un SDK .NET 10
aislado como `dotnet10`, sin reemplazar el runtime .NET 8 usado por las pruebas del núcleo:

```bash
sudo ./tools/bootstrap-wsl.sh
```

```bash
cd web
npm ci
npm test
npm run build
```

Vite genera `web/dist` con base `/Mis_widgets_feed/`. Fuera del Widgets Board, la página
muestra **Modo navegador** y mantiene deshabilitada la prueba nativa.

## GitHub y Pages

El repositorio conserva dos remotos con funciones distintas:

```text
origin  -> C:\Users\newsy\Proyectos Feed     (sincronización WSL → Windows)
github  -> https://github.com/josem404/Mis_widgets_feed.git
```

La publicación se activa con un push explícito:

```bash
git push github master
```

En GitHub, seleccionar **Settings → Pages → Build and deployment → GitHub Actions**.
El workflow prueba y compila la web antes de publicar exclusivamente `web/dist`. El
provider no debe desplegarse hasta que esta comprobación funcione:

```powershell
& '.\tools\verify-pages.ps1'
```

## Pruebas .NET desde WSL

El núcleo usa .NET 8 para que sus pruebas puedan ejecutarse en la copia canónica WSL:

```bash
dotnet test tests/MisWidgets.Feed.Tests/MisWidgets.Feed.Tests.csproj
```

El ejecutable MSIX sigue usando .NET 10 y Windows App SDK 2.4.0; su compilación y
ejecución solo se validan en Windows.

## Flujo nativo WSL → Windows

No sincronizar hasta que ambos árboles estén limpios y exista un commit en WSL. Después,
desde `C:\Users\newsy\Proyectos Feed`:

```powershell
& '.\tools\validate-and-deploy.ps1' -RestartWidgetHost
```

El script:

1. valida ambos árboles y sincroniza el commit WSL hacia Windows;
2. verifica que GitHub Pages devuelve la shell correcta sin cabeceras anti-frame;
3. ejecuta las pruebas .NET;
4. compila con MSBuild de Visual Studio;
5. registra una actualización MSIX en una ranura alterna, sin `Remove-AppxPackage`;
6. ejecuta `--selftest` bajo la identidad del paquete.

La automatización no puede confirmar por sí sola el comportamiento del host. La aceptación
final exige abrir el Board, habilitar **Mis Feed**, pulsar **Probar conexión**, cerrar y
reabrir el Board, y repetir tras deshabilitar y habilitar el feed.

### Estado de aceptación nativa

| Comprobación | Estado |
|---|---|
| Registro MSIX, catálogo de extensiones y self-test | Confirmado |
| Aparición de **Mis Feed** y control habilitar/deshabilitar | Confirmado |
| Carga de la shell desde GitHub Pages | Confirmado |
| `diagnostics.ping` y `diagnostics.pong` correlacionados | Confirmado (243 ms) |
| Cierre/reapertura, reactivación y ausencia de red | Pendiente de matriz sistemática |
| Shell empaquetada y servida íntegramente en local | Pendiente de sonda experimental |

## Diagnóstico

El provider escribe un log rotatorio de hasta 1 MiB en:

```text
%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalState\logs\feed.log
```

El self-test escribe `LocalState\selftest.txt`. Los objetos recibidos en callbacks WinRT no
se conservan fuera de la llamada y el proceso permanece bloqueado sin consumo activo hasta
que el host deshabilita todos los feeds del provider.
