# Delta: Persona-Skill Web Management — Catálogos en el form "Asignar"

## Purpose

Cerrar el bug del form "Asignar" de `/personas/{id:guid}/habilidades`: hoy los
`<select id="SkillId">` y `<select id="NivelHabilidadId">` muestran sólo el
placeholder porque el GET handler nunca consulta el catálogo de habilidades
activas ni el de niveles. Este delta añade carga paralela de catálogos en GET +
reload tras POST inválido, con degradación aceptable cuando la API de catálogo
cae por transporte.

## ADDED Requirements

### Requirement: Carga paralela de catálogos en GET

El handler GET de `/personas/{id:guid}/habilidades` MUST invocar en paralelo
las asociaciones existentes, el catálogo de habilidades activas y el catálogo
de niveles de habilidad antes de renderizar. Si alguna consulta de catálogo
falla por transporte, el GET MUST continuar y dejar la colección
correspondiente vacía para que la vista muestre sólo el placeholder.

#### Scenario: GET invoca los tres clientes en paralelo

- **DADO** una persona activa y catálogos poblados
- **CUANDO** un `Administrador` abre `/personas/{id:guid}/habilidades`
- **ENTONCES** el handler MUST invocar `IPersonaApiClient.GetSkillsAsync`, `IHabilidadApiClient.GetAllAsync` y `IHabilidadApiClient.GetNivelesHabilidadAsync` antes de devolver la página.

#### Scenario: Falla de transporte en un catálogo no aborta el GET

- **DADO** una persona activa y uno de los dos clientes de catálogo fallando por transporte
- **CUANDO** un `Administrador` abre la página
- **ENTONCES** el handler MUST devolver HTTP 200 con la grilla hidratada y la colección del catálogo fallido vacía.

### Requirement: Vista itera catálogos conservando el placeholder

Los `<select id="SkillId">` y `<select id="NivelHabilidadId">` del form
"Asignar" MUST renderizar su placeholder original como primera opción
seguido de una `<option>` por cada elemento del catálogo correspondiente.
La grilla de asociaciones activas MUST seguir renderizándose como antes, sin
verse afectada por este delta.

#### Scenario: Select de habilidad lista habilidades activas con placeholder

- **DADO** un catálogo de N habilidades activas devuelto por la API
- **CUANDO** la vista se renderiza
- **ENTONCES** `<select id="SkillId">` MUST contener N+1 `<option>` con la primera siendo `<option value="">Seleccionar habilidad...</option>`.

#### Scenario: Select de nivel lista niveles con placeholder

- **DADO** un catálogo de M niveles devuelto por la API
- **CUANDO** la vista se renderiza
- **ENTONCES** `<select id="NivelHabilidadId">` MUST contener M+1 `<option>` con la primera siendo `<option value="">Seleccionar nivel...</option>`.

#### Scenario: Grilla de asociaciones no se ve afectada por el delta

- **DADO** una persona con asociaciones activas precargadas
- **CUANDO** la vista se renderiza
- **ENTONCES** la tabla de habilidades MUST listar una fila por asociación, idéntica al comportamiento previo, mientras los `<select>` del form "Asignar" ahora muestran las opciones del catálogo.

### Requirement: POST preserva el comportamiento de asignación y baja

Los handlers `OnPostAsignarAsync` y `OnPostQuitarAsync` MUST seguir
agregando, actualizando y quitando asociaciones con el mismo contrato
observable que antes del delta, y el reload tras POST inválido MUST
repoblar también los catálogos para que los `<select>` sigan usables al
re-renderizar.

#### Scenario: Asignar con habilidad y nivel elegidos persiste la asociación

- **DADO** un `Administrador` que eligió una habilidad y un nivel del form
- **CUANDO** envía el POST
- **ENTONCES** el handler MUST invocar `IPersonaApiClient.UpsertSkillAsync` con los ids elegidos y MUST redirigir con `TempData` de éxito.

#### Scenario: POST inválido recarga los catálogos antes de re-renderizar

- **DADO** un `Administrador` que envió el form sin elegir habilidad ni nivel
- **CUANDO** el handler recibe el POST
- **ENTONCES** el reload MUST repoblar `HabilidadesDisponibles` y `NivelOptions` antes de devolver la página con los errores de `ModelState` visibles junto a cada `<select>`.

### Requirement: Degradación aceptable cuando la API de catálogo falla

Cuando la consulta al catálogo de habilidades o de niveles falla por
transporte, los `<select>` del form "Asignar" MUST renderizarse sólo con
su placeholder original, la página MUST seguir mostrando la grilla (o el
estado vacío) sin lanzar excepción, y MUST mostrarse un mensaje legible
de carga parcial vía el canal de feedback recuperable.

#### Scenario: Catálogo caído deja los <select> con sólo el placeholder

- **DADO** una persona activa y los clientes de catálogo fallando por transporte
- **CUANDO** un `Administrador` abre la página
- **ENTONCES** `<select id="SkillId">` y `<select id="NivelHabilidadId">` MUST contener sólo su placeholder original
- **Y** la página MUST mostrar un mensaje de carga parcial recuperable y MUST seguir renderizando la grilla.

#### Scenario: POST inválido durante catálogo caído no rompe la página

- **DADO** un `Administrador` que envía el form inválido mientras el catálogo está caído
- **CUANDO** el handler intenta recargar los catálogos
- **ENTONCES** la página MUST re-renderizarse con los `<select>` mostrando sólo el placeholder y los errores de `ModelState` originales, sin propagar la excepción.

### Requirement: ViewModel expone las colecciones de catálogo

`PersonaHabilidadesViewModel` MUST exponer `HabilidadesDisponibles`
(`IReadOnlyList<HabilidadListItemViewModel>`) y `NivelOptions`
(`IReadOnlyList<NivelHabilidadDto>`) pobladas desde el GET para que la
vista pueda iterarlas en los `<select>`.

#### Scenario: ViewModel expone colecciones pobladas tras GET exitoso

- **DADO** un GET con catálogos de N habilidades y M niveles
- **CUANDO** la página se renderiza
- **ENTONCES** `Model.ViewModel.HabilidadesDisponibles.Count` MUST ser N y `Model.ViewModel.NivelOptions.Count` MUST ser M.

## MODIFIED Requirements

(ninguno — los requirements previos de la spec base — acceso restringido,
listado/baja, persona inactiva, cliente tipado, errores PRG y descubribilidad
— mantienen su comportamiento observable; este delta es estrictamente
aditivo.)

## REMOVED Requirements

(ninguno.)

## RENAMED Requirements

(ninguno.)