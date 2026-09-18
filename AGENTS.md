# AGENTS.md — proyecto independiente de Feed Provider

## Identidad y alcance

`Proyectos_feed_WSL` es un proyecto independiente de `Proyectos_widgets_WSL`.
Tiene su propio repositorio, historial, decisiones, compilación, despliegue y
validación. No se deben mezclar commits ni asumir que una modificación en un
proyecto actualiza el otro.

El objetivo inicial es investigar y, más adelante, implementar una experiencia
de Feed Provider para Windows Widgets que pueda funcionar como un cajón o tablón
personal de notas y elementos heterogéneos. La investigación inicial está en
`Investigación paneles y dashboards del Widgets Board.md`.

## Copias canónicas

| Copia | Ruta | Uso |
|---|---|---|
| WSL | `/home/ubunewsys/Proyectos_feed_WSL` | Edición, documentación, Git y análisis |
| Windows | `C:\Users\newsy\Proyectos Feed` | Compilación, registro, ejecución y pruebas nativas |

El proyecto de widgets relacionado conserva sus propias copias:

- WSL: `/home/ubunewsys/Proyectos_widgets_WSL`
- Windows: `C:\Users\newsy\Proyectos Widgets`

## Relación con Proyectos_widgets_WSL

Los dos proyectos pueden interoperar en el futuro para consultar archivos y
datos de ambos, pero la relación debe ser explícita y de bajo acoplamiento:

- no copiar código o modelos entre repositorios sin una decisión documentada;
- definir contratos de lectura, rutas y permisos antes de implementarlos;
- validar que una operación de un proyecto no deje al otro bloqueado;
- mantener separadas las identidades de paquete, procesos, configuración y
  estado persistente;
- cualquier acción nativa solicitada desde una superficie web debe pasar por
  comandos tipados, validación y confirmaciones cuando corresponda.

La idea funcional es que el feed pueda consultar elementos del proyecto de
widgets y que los widgets puedan abrir o consultar elementos del feed, pero no
se debe convertir el Widgets Board en un servicio residente. El trabajo debe
ser bajo demanda y mantener el Board ligero.

## Flujo de trabajo WSL → Windows

1. Editar y revisar únicamente en esta copia WSL.
2. Confirmar `git status --short` y no sincronizar una copia Windows sucia.
3. Crear un commit en WSL antes de sincronizar.
4. Enviar el commit a la copia Windows mediante el remoto local `origin`.
5. En Windows, actualizar la copia con `git reset --hard HEAD` **solo después
   de comprobar que el árbol Windows está limpio**.
6. Ejecutar desde la copia Windows las tareas nativas: MSBuild, registro MSIX,
   Widget Board, pruebas visuales y self-test.
7. Si se hacen cambios excepcionales en Windows, traerlos explícitamente a WSL
   antes de continuar con `git pull origin master`.

Comandos de referencia, ejecutados desde WSL:

```bash
git status --short
git add .
git commit -m "Documentar investigación inicial del feed provider"
git push origin master
```

Para sincronizar la copia Windows, usar PowerShell y comprobar antes el estado:

```powershell
git -C 'C:\Users\newsy\Proyectos Feed' status --short
git -C 'C:\Users\newsy\Proyectos Feed' reset --hard HEAD
git -C 'C:\Users\newsy\Proyectos Feed' status --short
```

La copia Windows no es la fuente de edición. No usar `Remove-AppxPackage` ni
comandos destructivos como parte de una iteración normal. Las pruebas nativas
solo se consideran completadas después de observar su resultado en Windows.

## Estado actual

- Investigación documental inicial incorporada.
- Primera rebanada vertical implementada en WSL: un feed HTML, ciclo COM y puente de
  diagnóstico tipado, sin persistencia ni diseño del dashboard.
- Shell web destinada a GitHub Pages en
  `https://josem404.github.io/Mis_widgets_feed/`.
- El código y el estado siguen completamente separados de `Proyectos_widgets_WSL`; solo
  se han copiado provisionalmente sus tres assets de identidad visual, sin vínculo en
  compilación o ejecución.
- Pendiente: publicación de Pages, compilación/despliegue nativo y aceptación visual y
  funcional en el Widgets Board de Windows.
