# Especificación: Split de VacanteInputModel para Create y Edit

## Propósito

Eliminar el workaround `ModelState.Remove("Input.EstadoVacanteId")` en `Create.cshtml.cs` separando `VacanteInputModel` en `VacanteCreateInputModel` (sin `EstadoVacanteId`) y `VacanteEditInputModel` (con `EstadoVacanteId` `[Required]`). Cada formulario bindea exactamente el tipo que necesita. Decisión D-3.

## Requisitos

### Requisito: Create usa modelo sin EstadoVacanteId

`src/SGV.Contracts/Vacantes/Modelos/VacanteCreateInputModel.cs` NO DEBE contener una propiedad `EstadoVacanteId`. La página `Create` (`src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs`) DEBE declararse con `[BindProperty] public VacanteCreateInputModel Input { get; set; }`.

#### Escenario: Tipo Create sin EstadoVacanteId

- **DADO** que el archivo `VacanteCreateInputModel.cs` existe en `src/SGV.Contracts/Vacantes/Modelos/`
- **CUANDO** se inspecciona el tipo por reflexión
- **ENTONCES** NO DEBE existir propiedad `EstadoVacanteId` en el tipo.

#### Escenario: Create PageModel bindea VacanteCreateInputModel

- **DADO** la página `Vacantes/Create.cshtml.cs`
- **CUANDO** se inspecciona la propiedad `Input`
- **ENTONCES** su tipo declarado DEBE ser `VacanteCreateInputModel`.

### Requisito: Edit usa modelo con EstadoVacanteId Required

`src/SGV.Contracts/Vacantes/Modelos/VacanteEditInputModel.cs` DEBE contener una propiedad `EstadoVacanteId` de tipo `Guid?` decorada con `[Required]`. La página `Edit` DEBE declararse con `[BindProperty] public VacanteEditInputModel Input { get; set; }`.

#### Escenario: Tipo Edit con EstadoVacanteId Required

- **DADO** `VacanteEditInputModel.cs`
- **CUANDO** se inspecciona la propiedad `EstadoVacanteId` por reflexión
- **ENTONCES** su tipo DEBE ser `Guid?`
- **Y** DEBE tener el atributo `[Required]`.

#### Escenario: Edit PageModel bindea VacanteEditInputModel

- **DADO** la página `Vacantes/Edit.cshtml.cs`
- **CUANDO** se inspecciona la propiedad `Input`
- **ENTONCES** su tipo declarado DEBE ser `VacanteEditInputModel`.

### Requisito: Ausencia de ModelState.Remove en Create

`Create.cshtml.cs` NO DEBE contener invocaciones a `ModelState.Remove` para campos del `Input`.

#### Escenario: Ausencia de ModelState.Remove en Create

- **DADO** el archivo `Create.cshtml.cs`
- **CUANDO** `grep -n "ModelState.Remove" src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` se ejecuta
- **ENTONCES** el resultado DEBE ser vacío.

### Requisito: Create POST envía EstadoVacanteId null al API

El handler de POST de Create DEBE serializar el request al API con `EstadoVacanteId = null` (el API lo resuelve al estado inicial sembrado). El wire behavior no cambia.

#### Escenario: Create POST serializa null

- **DADO** el modelo `VacanteCreateInputModel` populado (sin `EstadoVacanteId`)
- **CUANDO** el handler serializa el request al endpoint POST
- **ENTONCES** el body JSON DEBE contener `estadoVacanteId: null`.

### Requisito: Edit POST envía EstadoVacanteId al API

El handler de POST de Edit DEBE serializar el request al API con `EstadoVacanteId = <Guid>`.

#### Escenario: Edit POST serializa EstadoVacanteId

- **DADO** `VacanteEditInputModel.EstadoVacanteId` populado con un GUID
- **CUANDO** el handler serializa el request al endpoint PATCH
- **ENTONCES** el body JSON DEBE contener `estadoVacanteId` con el valor del modelo.
