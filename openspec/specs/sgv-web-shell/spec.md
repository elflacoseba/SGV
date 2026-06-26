# SGV Web Shell Specification

## Purpose

Define the first functional Razor Pages frontend shell for `SGV.Web`: a neutral Inspinia-based layout that is ready for incremental SGV modules without exposing demo content, authentication flows, or placeholder business screens.

## Requirements

### Requirement: Functional base shell

The system MUST provide a functional SGV web shell using the Inspinia Starterkit baseline, including a shared layout, static assets, page chrome, and a reachable default page.

#### Scenario: Shell loads successfully

- GIVEN the SGV web application is running
- WHEN a user opens the default web entry point
- THEN the response MUST render an SGV shell page successfully
- AND the page MUST load the required shared styling and scripts

#### Scenario: Missing optional module content

- GIVEN no SGV business modules have been implemented in this shell
- WHEN the default page is rendered
- THEN the page MUST remain usable without requiring module-specific data

### Requirement: Demo content removal

The system MUST NOT expose Inspinia demo pages, sample dashboards, fake data screens, or demo navigation as product-facing content.

#### Scenario: Demo navigation is absent

- GIVEN the shell navigation is rendered
- WHEN a user reviews the available navigation entries
- THEN no entry MUST link to Inspinia demo pages, sample dashboards, or fake module screens

#### Scenario: Demo pages are not reachable from shell chrome

- GIVEN the shell page is rendered
- WHEN a user follows visible shell links
- THEN the user MUST NOT be taken to template demo content

### Requirement: Minimal technical navigation

The system MUST include only minimal technical navigation required for the shell, and MUST NOT include placeholder SGV business modules before those modules are specified.

#### Scenario: Technical navigation only

- GIVEN the navigation menu is displayed
- WHEN a user inspects the menu
- THEN entries MUST be limited to shell-level technical destinations
- AND no recruitment, vacancy, organizational, or catalog placeholder modules MUST appear

### Requirement: Neutral branding and Inspinia visual system

The system MUST use neutral `SGV` branding, SHOULD preserve Inspinia template colors, and MUST preserve Inspinia layout controls/customizer for this first delivery.

#### Scenario: Neutral SGV brand is visible

- GIVEN the shell is rendered
- WHEN a user views the header, sidebar, or browser title
- THEN the visible product identity MUST be neutral `SGV`

#### Scenario: Layout controls remain available

- GIVEN the shell layout is rendered
- WHEN a user accesses available layout controls or customizer affordances
- THEN the Inspinia layout controls MUST remain present and functional

### Requirement: No authentication dependency

The system MUST NOT require authentication, authorization policies, login UI, registration UI, or account-management navigation for this shell delivery.

#### Scenario: Public shell access

- GIVEN an unauthenticated user
- WHEN the user opens the default SGV web shell
- THEN the shell MUST render without redirecting to login

#### Scenario: Account UI is absent

- GIVEN the shell chrome is rendered
- WHEN a user reviews visible links and actions
- THEN login, registration, logout, and account-management options MUST NOT be shown

### Requirement: Frontend validation expectations

The implementation MUST keep the Razor Pages shell buildable and MUST validate the frontend asset workflow when shell assets or asset pipeline configuration are changed.

#### Scenario: .NET solution remains buildable

- GIVEN the shell implementation is complete
- WHEN the solution build is executed
- THEN the build MUST succeed without backend, database, or API changes required by this shell

#### Scenario: Asset pipeline changes are validated

- GIVEN frontend assets or asset pipeline configuration are modified
- WHEN the frontend asset build command is executed
- THEN generated shell assets MUST compile successfully
