---
title: Operación del negocio
summary: Cómo trabaja la empresa con sus clientes, lotes, campañas, cultivos, labores, órdenes de trabajo, análisis y proveedores.
source: Documento de referencia de los ingenieros (Google Docs)
reviewed: 2026-11-28
version: 1
---

# Operación del negocio

## Para qué sirve este documento

Este documento describe la actividad de la empresa tal como la definieron sus ingenieros agrónomos: con quién se
trabaja, sobre qué unidades productivas, en qué ciclo y con qué tareas. Es la referencia del negocio para el personal
de soporte y para el personal actual y futuro de la compañía.

Las ayudas de cada pantalla explican **cómo** se usa el sistema; este documento explica **qué** operación respalda.
Es una versión editada de la referencia de los ingenieros: mantiene sus datos y listas, con la ortografía corregida y
una estructura más fácil de leer.

## Bases de datos generales

### Clientes

De cada cliente se registran todos sus datos:

- Firma comercial.
- Nombre del productor o de los productores que integran la sociedad.
- Mail.
- Celular.
- Dirección.
- CUIT.

Un cliente puede estar integrado por **varias firmas comerciales**.

### Campos y lotes

Cada cliente tiene campos, que según su tenencia pueden ser:

| Tenencia | Descripción |
| --- | --- |
| Alquiler fijo | Campo alquilado a un valor fijo. |
| Alquiler a porcentaje | Campo alquilado a porcentaje. |
| Propio | Campo propiedad del cliente. |

- Un campo puede tener un único lote o más de uno.
- Cada lote tiene una superficie, que puede cambiar entre campañas.

### Campañas

La **campaña agrícola** comienza en mayo de un año y termina en mayo del año siguiente. Por ejemplo, este año
transcurre la campaña 2026-27.

En algunos lotes ese límite de meses de inicio y fin no se cumple: la campaña puede iniciar antes o finalizar más
tarde. Por lo tanto, habría que asignarle una campaña al cultivo.

### Cultivos

Sobre los lotes se producen **cultivos de renta** o **cultivos de servicio**. En una campaña agrícola, un lote puede
tener:

- Un solo cultivo.
- Dos cultivos, ambos de renta.
- Dos cultivos, uno de servicio y el otro de renta.

Además:

- En el caso de productores que tienen ganadería, también se pueden sembrar **pasturas**, que suelen durar más de una
  campaña.
- El cultivo de servicio puede utilizarse como pastoreo; en ese caso se denomina **verdeo de invierno** o **verdeo de
  verano**.

**Base de datos de cultivos.** En cada lote se pueden implantar diferentes cultivos, como:

- Trigo
- Cebada
- Avena
- Garbanzo
- Lenteja
- Colza
- Camelina
- Carinata
- Arveja verde
- Arveja amarilla
- Centeno
- Triticale
- Vicia
- Nabo
- Pasturas puras de gramíneas
- Pasturas puras de leguminosas
- Pasturas consociadas
- Raigrás
- Soja
- Maíz
- Maíz pisingallo
- Sorgo granífero
- Sorgo forrajero
- Sorgo silero
- Maíz silo
- Girasol
- Poroto mungo

Esta lista puede ir cambiando e incorporando nuevos cultivos, por lo que debe ser personalizable.

**Historial del lote.** El cultivo de cada campaña en cada lote debe quedar registrado en el historial del lote, al
igual que otros datos, como el rendimiento promedio de cosecha, tanto de grano como de biomasa de forraje.

### Labores y etapas

El trabajo de los ingenieros sobre los lotes consiste en recomendar distintas tareas a realizar, que se denominan
**labores**. Las labores se realizan en distintos momentos del ciclo del cultivo, llamados **etapas**:

| Etapa | Momento |
| --- | --- |
| Presiembra | Antes de la siembra. |
| Preemergencia | Desde la siembra hasta que nace el cultivo. |
| Postemergencia | Después del nacimiento del cultivo. |

Las labores son las siguientes, agrupadas como en la referencia de los ingenieros. *Los títulos de los grupos que no
tienen nombre en la referencia son descriptivos.*

**Lista principal**

- Pulverización terrestre
- Pulverización aérea / avión
- Pulverización aérea / dron
- Fertilización al voleo proyectada
- Fertilización al voleo neumática
- Fertilización incorporada
- Fertilización líquida
- Disco doble acción
- Acondicionador de suelos
- Cincel con rastra, peine o rolo
- Rastra de dientes
- Rastra de dientes con rolo
- Rolo
- Disco + rastra de dientes + rolo
- Siembra fina con fertilización
- Siembra de gruesa a placa con fertilización simple
- Siembra de gruesa neumática con fertilización simple
- Siembra de gruesa a placa con fertilización doble
- Siembra de gruesa neumática con fertilización doble
- Siembra de gruesa a chorrillo sin fertilización
- Siembra de gruesa a chorrillo con fertilización
- Cosecha

**Labranza y preparación del suelo**

- Subsolado / Descompactado profundo (paratill o subsolador de púas)
- Rastra de discos pesada (excéntrica o "rastra de tiro excéntrico")
- Cultivador de campo (con rolo o rastra de dientes)
- Vibrocultivador (con peine o rolo desterronador)
- Rastra rotativa (de eje vertical u horizontal / rotovator)
- Nivelación / Emparejamiento con rabasto
- Nivelación láser / Nivelación con pala de arrastre (sistematización y drenaje)
- Sistematización de curvas de nivel / Armado de terrazas
- Borrado de huellas (de cosechadora o pulverizadora)
- Picado / Triturado de rastrojo (desmalezadora de hélice o trituradora de martillos)
- Rolado de cultivos de cobertura / Rolado de rastrojos (rolo faca / rolo picador)

**Siembra e implantación (variantes faltantes)**

- Siembra directa fina sin fertilización
- Siembra directa de gruesa a placa sin fertilización
- Siembra directa de gruesa neumática sin fertilización
- Siembra con dosificación variable (semilla o fertilizante, o ambos)
- Siembra al voleo (pasturas o cultivos de cobertura)
- Interseeding / Siembra aérea de cultivos de servicio (con avión o dron sobre cultivo en pie)
- Siembra variable
- Inoculación y curado de semillas a campo (si se factura como labor aparte)

**Protección de cultivos**

- Pulverización terrestre selectiva (sensores ópticos tipo WeedSeeker / Weed-It)
- Escardillado / Carpida mecánica (cultivador entre surcos)
- Control de plagas con cebos tóxicos (aplicación al voleo para hormigas, babosas o tucuras)

**Fertilización y enmiendas**

- Fertilización al voleo con dosis variable (ambientación)
- Fertilización líquida chorreada (en superficie)
- Fertilización variable
- Fertilización líquida inyectada / incorporada (por ejemplo, UAN o amoníaco anhidro)
- Aplicación de enmiendas calcáreas / Yeso agrícola (encalado)
- Aplicación de enmiendas orgánicas (estiércol sólido, cama de pollo o compost con estercolera)
- Aplicación de efluentes líquidos / Purines (con tanque estercolero e inyector)
- Fertilización foliar (cuando se realiza como pasada específica sin fitosanitarios)

**Cosecha, poscosecha y logística en el lote**

- Cosecha fina (trigo, cebada, colza, etc.)
- Cosecha gruesa (soja, maíz, girasol, sorgo)
- Cosecha con plataforma draper / cabezal maicero / cabezal girasolero (si se diferencia la tarifa por cabezal)
- Hilerado / Corte e hilerado (cosecha en dos tiempos, por ejemplo, colza, pasturas o poroto)
- Recolección / Trilla de cordón (con cabezal recolector)
- Acarreo en el lote / Movimiento de tolva autodescargable (logística interna hasta el camión o el silo)
- Embolsado de grano seco / húmedo (con embolsadora)
- Extracción de grano de silobolsa (con extractora)
- Cosecha de grano húmedo (para quebrado y embolsado)

**Forrajes y ganadería** (opcional, si el campo es mixto)

- Corte de pasturas (segadora de discos o de hélice)
- Corte con acondicionador (segadora acondicionadora de rodillos o de dedos)
- Rastrillado / Hilerado de forraje (rastrillo estelar o de entrega lateral)
- Confección de rollos (rotoenfardadora)
- Confección de megafardos / fardos prismáticos
- Encintado / Plastificado de rollos (henolaje)
- Picado fino de forraje (silaje de maíz, sorgo o pasturas)
- Compactación y tapado de silo (silo puente o torta)
- Desmalezado / Limpieza de potreros
- Embolsado de grano
- Extracción de grano

**Mantenimiento de infraestructura rural**

- Mantenimiento de caminos internos / Cuneteo (motoniveladora o disco)
- Limpieza de canales y desagües (retroexcavadora)
- Desmonte selectivo / Rolado de renovales
- Mantenimiento de alambrados y cortafuegos

### Órdenes de trabajo

Las labores recomendadas se realizan a través de una **orden de trabajo**.

- La orden requiere un dato de entrada: un **nombre corto**, que es el identificador (ID) de cada unidad productiva.
- El nombre corto está integrado por el nombre del cliente, del campo y del lote.
- El lote puede ser nuevo, venir de campañas anteriores del mismo productor o cambiar de productor.

### Agroquímicos

Agroquímicos que pueden utilizarse y recomendarse en las tareas a realizar en el campo:

- Fertilizantes
- Herbicidas
- Insecticidas
- Fungicidas
- Coadyuvantes
- Curasemillas
- Bioestimulantes
- Terápicos de semillas
- Inoculantes

## Análisis de suelos

Periódicamente, los ingenieros recomiendan en los lotes la extracción de muestras y el análisis de suelos para
distintos nutrientes o parámetros del suelo.

**Registro**

- El resultado del análisis debe quedar registrado en el lote y en la campaña correspondientes.
- Debe registrarse la fecha del análisis de suelo.

**Uso del dato**

- El dato se puede utilizar para tomar decisiones de fertilización en esa misma campaña o en otra, ya que ciertos
  parámetros son estables en el tiempo.
- Por lo tanto, debe ser fácil acceder a esa información, incluso desde otras campañas, y poder seguir cargando
  análisis de suelos en las campañas nuevas.

**Muestreo**

- El análisis puede realizarse sobre el promedio del lote o por estaciones de muestreo predefinidas, por lo que debe
  quedar registrado el detalle de cómo se realizó el muestreo.
- Puede incluir un enlace al archivo shape o KML que muestre la zona de muestreo.

## Análisis de semillas

Todos los cultivos a implantar tienen datos específicos de calidad de semilla:

| Dato | Detalle |
| --- | --- |
| Peso de 1000 | Peso de mil semillas. |
| Poder germinativo con curasemilla | Detallando plántulas anormales, plántulas muertas y semillas duras. |
| Poder germinativo sin curasemilla | Detallando plántulas anormales, plántulas muertas y semillas duras. |
| Vigor | — |

- Estos datos deben quedar registrados según el cultivo y la variedad o el híbrido de cada cultivo, y según la
  procedencia de cada productor.
- A veces no se cuenta con esa información.
- Con estos datos se define la población de semillas a sembrar, en kg/ha o en semillas/ha, para enviar la información
  a los clientes.

## Proveedores

### Proveedores de insumos de los clientes

Los clientes consultan y compran en diferentes proveedores. De ellos se registran:

- Las firmas comerciales.
- Sus representantes de ventas y sus representantes técnicos.
- Los precios de referencia de los productos con que se cuenta.

A veces los clientes envían distintos presupuestos de diferentes proveedores y productos, y los ingenieros deben
evaluar cuál conviene y por qué motivos.

### Proveedores de servicios

Listado de proveedores de servicios:

- Contratistas.
- Laboratorios de suelos y de semillas.
- Plataformas virtuales.

De cada uno se registran los contactos, la web, etc.

## Glosario

| Término | Significado en este documento |
| --- | --- |
| Agroquímico | Producto que puede utilizarse y recomendarse en las tareas del campo; se agrupa en las categorías de la sección Agroquímicos. |
| Campaña agrícola | Período que comienza en mayo de un año y termina en mayo del año siguiente, por ejemplo 2026-27. En algunos lotes puede iniciar antes o finalizar más tarde. |
| Campo | Tierra de un cliente, propia o alquilada a alquiler fijo o a porcentaje; tiene uno o más lotes. |
| Cultivo de renta | Cultivo que se produce en un lote; un lote puede tener uno o dos cultivos de renta en una campaña. |
| Cultivo de servicio | Cultivo que se produce en un lote, distinto del de renta; puede utilizarse como pastoreo (verdeo). En una campaña, un lote puede tener uno de servicio y otro de renta. |
| Estación de muestreo | Forma predefinida de muestrear un lote para el análisis de suelo, como alternativa al promedio del lote. |
| Etapa | Momento del ciclo del cultivo en que se realiza una labor: presiembra, preemergencia o postemergencia. |
| Firma comercial | Dato de un cliente o de un proveedor que se registra en su base de datos; un cliente puede estar integrado por varias firmas comerciales. |
| Historial del lote | Registro, por campaña, de los cultivos de un lote y de otros datos, como el rendimiento promedio de cosecha de grano y de biomasa de forraje. |
| Labor | Tarea que los ingenieros recomiendan realizar en un lote. |
| Lote | Parte de un campo con una superficie que puede cambiar entre campañas; sobre él se producen los cultivos. |
| Nombre corto | Identificador (ID) de cada unidad productiva, integrado por el nombre del cliente, del campo y del lote; es el dato de entrada de la orden de trabajo. |
| Orden de trabajo | Documento a través del cual se realizan las labores recomendadas. |
| Pastura | Cultivo que suele durar más de una campaña; lo siembran productores que tienen ganadería. |
| Peso de 1000 | Peso de mil semillas; dato de calidad de semilla. |
| Poder germinativo | Dato de calidad de semilla, con o sin curasemilla, que detalla plántulas anormales, plántulas muertas y semillas duras. |
| Población de semillas | Cantidad a sembrar, en kg/ha o en semillas/ha, definida a partir de los datos de calidad de semilla. |
| Precio de referencia | Precio de un producto que se registra junto con los proveedores de insumos. |
| Presupuesto | Lo que un cliente recibe de diferentes proveedores y productos y envía a los ingenieros, que evalúan cuál conviene y por qué. |
| Verdeo | Cultivo de servicio utilizado como pastoreo; puede ser de invierno o de verano. |
