# Plan de Refactorización y Actualización (Ejecución Secuencial)

Por favor, ejecuta las siguientes fases en orden estricto. No pases a la siguiente fase sin haber completado y validado los criterios de aceptación de la anterior.

## Fase 1: Migración a .NET 10
**Objetivo:** Actualizar la solución completa al nuevo framework LTS aprovechando sus mejoras de rendimiento y compilación AOT.
* **Acción 1.1:** Actualizar los archivos `.csproj` cambiando el `<TargetFrameworks>` a `net10.0-android;net10.0-ios;net10.0-maccatalyst` (según corresponda).
* **Acción 1.2:** Actualizar todos los paquetes NuGet de Microsoft, dependencias de MAUI, Blazor, IdentityModel y Polly a sus versiones estables compatibles con .NET 10.
* **Acción 1.3:** Resolver cualquier *Warning* o *Error* de compilación derivado de APIs obsoletas entre .NET 9 y .NET 10.

## Fase 2: Centralización del Manejo de Sesión (OAuth)
**Objetivo:** Eliminar la validación manual de sesión en cada página Razor y delegarla al ciclo de vida de Blazor.
* **Acción 2.1:** Implementar o extender un `AuthenticationStateProvider` personalizado que valide el token OAuth y el estado de la sesión de manera global.
* **Acción 2.2:** Configurar el enrutador en `App.razor` (o `Routes.razor`) usando `<CascadingAuthenticationState>` y `<AuthorizeRouteView>`.
* **Acción 2.3:** Eliminar el código repetitivo de verificación de sesión (ej. chequeos en el método `OnInitializedAsync`) de todas las páginas `.razor` individuales.
* **Acción 2.4:** Garantizar que las nuevas páginas creadas en el futuro estén protegidas por defecto utilizando el atributo `[Authorize]` a nivel de página o en el archivo `_Imports.razor`.

## Fase 3: Unificación de Diseño UI y Eliminación de DevExpress
**Objetivo:** Reemplazar componentes pesados por HTML/CSS nativo y unificar el sistema de diseño.
* **Acción 3.1:** Auditar todos los componentes `.razor` e identificar el uso de etiquetas de DevExpress (ej. `<DxGrid>`, `<DxButton>`, etc.).
* **Acción 3.2:** Reemplazar estos componentes por tablas, botones y formularios HTML estándar, utilizando las clases CSS existentes de la aplicación para mantener el diseño visual idéntico.
* **Acción 3.3:** Extraer todos los estilos repetitivos de los archivos Scoped CSS (`NombrePagina.razor.css`) y consolidarlos en un archivo global (ej. `wwwroot/css/app.css` o `theme.css`).
* **Acción 3.4:** Dejar en los archivos `.razor.css` únicamente reglas altamente específicas que no se reutilicen en ninguna otra parte de la app.

## Fase 4: Refactorización de Componentes Core (Layout y Offline)
**Objetivo:** Mejorar la navegación superior y la experiencia sin conexión.
* **Acción 4.1 (Top App Bar):** Modificar el `MainLayout.razor` (o el componente de Cabecera). 
    * Inyectar `NavigationManager` para detectar la ruta actual.
    * **Lógica de UI:** Si la ruta es `/` (Home), mostrar el menú hamburguesa a la izquierda. Si la ruta es diferente a `/`, mostrar un botón/flecha de "Volver Atrás" en el lado izquierdo.
    * Colocar el Título de la página actual centrado en la barra superior.
* **Acción 4.2 (Pantalla Offline):** Localizar el componente o vista que maneja el estado "Sin conexión" de la red.
    * Reescribir su estructura HTML y aplicarle las clases CSS del nuevo tema global consolidado en la Fase 3, para que visualmente pertenezca a la misma aplicación.
    * Asegurar que el mensaje sea claro, amigable e indique el estado de la conexión en tiempo real si es posible.