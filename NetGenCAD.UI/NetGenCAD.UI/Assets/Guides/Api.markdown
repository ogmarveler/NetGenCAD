# ![Logo](Assets/Images/logo.png) NetGenCAD API Documentation
###### 
#### Description
* Welcome to the NetGenCAD API Documentation! The API is used for creating and managing custom axes, solids, and objects used in OpenSCAD, a solid 3D CAD modeler. 
* The API can be used for existing CAD migrations from other applications, or allow multiple team members in an intranet to work on the same objects.
###### 
#### How It Differs From the UI
* The API provides programmatic use of the NetGenCAD application The Axes and Object designer modules are available through the API, with the Shape Designer still being UI only as of this iteration. 
* Whereas UI functions as both a UX and UI, the API allows for use of the UI as a data visualization tool. API requests will update in real-time, and a user can make manual adjustments through the UI or API.
###### 
#### Getting Started
* The API is available in both REST and gRPC formats. The server automatically starts when NetGenCAD is running at `http://localhost:5000`. 
* Open the top-bar API indicator on the right side to see this address and the Windows named pipe or Linux Unix socket. 
###### 
* ![Logo](Assets/Images/offline.png) **Offline** means it is starting, stopped, or unreachable.
* ![Logo](Assets/Images/local.png) **Local** means the localhost server responded.
* ![Logo](Assets/Images/online.png) **Online** is reserved for future network access and is not currently used.
###### 
#### Understanding the Documentation
* The tables below list all fields in each request message. Optional fields may be omitted. 
* For native gRPC, populate the corresponding protobuf request rather than sending an HTTP JSON body.
* Send JSON request bodies with `Content-Type: application/json`. 
* Field names can be written in camelCase or snake_case; enum names such as `METRIC` and `CUBE` or their numeric values are accepted. 
###### 
###### 
###### 
## Axis Endpoints
###### 
| Method (REST) | Path (REST) | Method (gRPC) | Purpose |
| :---: | :---: | :---: | --- |
| GET | /v1/axes | AxesService.GetAxesList | Get metric and imperial axes. |
| GET | /v1/axes/convertUnits | AxesService.ConvertUnits | Convert axis values between metric and imperial units. |
| POST | /v1/axes/createUpdate | AxesService.CreateUpdateAxis | Create or update an axis. |
| DELETE | /v1/axes/deleteAxis | AxesService.DeleteAxis | Delete an axis by axisId or axisCallingMethod. |
###### 
###### 
### Get Axes
###### 
* #### `GET /v1/axes` (REST)
* #### `AxesService.GetAxesList` (gRPC)

* Its optional query parameter is also the only field in the gRPC `GetAxesListRequest`.
* Use `/v1/axes?axisId=1` (or `axis_id=1`) for one axis, including OpenSCAD definitions. The full list omits `axisOscad` and `axesOscad`.
* The equivalent gRPC request contains `axisId: 1`; use an empty request for all axes. 
###### 
###### 
| Field | Type | Use | Example |
| --- | :---: | --- | --- |
| axisId | integer | Optional positive ID; omit to list all axes. | ?axisId=1 |
###### 
###### 
### Convert Units
###### 
* #### `GET /v1/axes/convertUnits` (REST)
* #### `AxesService.ConvertUnits` (gRPC)

* Reads a JSON body `ConvertUnitsRequest`. 
* Native gRPC clients can call `AxesService.ConvertUnits` at `/createAxes.AxesService/ConvertUnits` instead.
###### 
###### 
| Field | Type | Use | JSON example |
| --- | :---: | --- | --- |
| minX, maxX | number | X bounds. | "minX": 0, "maxX": 13 |
| minY, maxY | number | Y bounds. | "minY": 0, "maxY": 13 |
| minZ, maxZ | number | Z bounds. | "minZ": 0, "maxZ": 13 |
| volume | number | Cubic centimeters or cubic inches. | "volume": 0 |
| volumeScale | number | Cubic meters or cubic feet. | "volumeScale": 0 |
| decimalPlaces | integer | Rounding precision, 0 through 15. | "decimalPlaces": 3 |
| fromUnit | enum | METRIC or IMPERIAL. | "fromUnit": "METRIC" |

```json
    {
      // Request Body ConvertUnitsRequest
      "minX": 0, "maxX": 13,
      "minY": 0, "maxY": 13,
      "minZ": 0, "maxZ": 13,
      "volume": 0, "volumeScale": 0,
      "decimalPlaces": 3,
      "fromUnit": "METRIC"
    }
```
###### 
###### 
### Create or Update an Axis
###### 
* #### `POST /v1/axes/createUpdate` (REST)
* #### `AxesService.CreateUpdateAxis` (gRPC)

* Reads a JSON body `CreateCustomAxisRequest`. 
* The response includes `axisId` and `callingMethod`.
###### 
###### 
| Field | Type | Use | JSON example |
| --- | :---: | --- | --- |
| minX, maxX | number | X bounds. | "minX": 0, "maxX": 100 |
| minY, maxY | number | Y bounds. | "minY": 0, "maxY": 100 |
| minZ, maxZ | number | Z bounds. | "minZ": 0, "maxZ": 100 |
| selectedUnit | enum | METRIC or IMPERIAL. | "selectedUnit": "METRIC" |
| selectedBackground | enum | LIGHT or DARK. | "selectedBackground": "LIGHT" |
| decimalPlaces | integer | Optional; defaults to 3. | "decimalPlaces": 3 |
| colorHex | string | Optional #RGB or #RRGGBB; takes precedence over oscadColor. | "colorHex": "#336699" |
| oscadColor | string | Optional named OpenSCAD color. | "oscadColor": "Black" |
| axisColorAlpha | number | Optional opacity from 0 to 1; defaults to 1. | "axisColorAlpha": 1 |

```json
    {
      // Request Body CreateCustomAxisRequest
      "minX": 0, "maxX": 100,
      "minY": 0, "maxY": 100,
      "minZ": 0, "maxZ": 100,
      "selectedUnit": "METRIC",
      "selectedBackground": "LIGHT",
      "decimalPlaces": 3,
      "colorHex": "#336699",
      "oscadColor": "Black",
      "axisColorAlpha": 1
    }
```
###### 
###### 
### Delete an Axis
###### 
* #### `DELETE /v1/axes/deleteAxis` (REST)
* #### `AxesService.DeleteAxis` (gRPC)

* Reads a JSON body `DeleteAxisRequest`. 
* Specify an existing axis ID or calling method. 
* The ID takes precedence if both are supplied; omit either field when using the other.
###### 
###### 
| Field | Type | Use | JSON example |
| --- | :---: | --- | --- |
| axisId | integer | Optional axis ID. | "axisId": 1 |
| axisCallingMethod | string | Optional axis calling method. | "axisCallingMethod": "axis_name" |

```json
    {
      // Request Body DeleteAxisRequest
      "axisId": 1,
      "axisCallingMethod": "axis_name"
    }
```
###### 
###### 
###### 
## Object Endpoints
###### 
| Method (REST) | Path (REST) | Method (gRPC) | Purpose |
| :---: | :---: | :---: | --- |
| POST | /v1/object/solid/createUpdate | ObjectScadService.CreateUpdateSolid | Create or update a solid. |
| DELETE | /v1/object/solid/deleteItem | ObjectScadService.DeleteItem | Delete a solid or module using itemType and itemId. |
| GET | /v1/object/get | ObjectScadService.GetObject | Get an object and its solid and module summaries by objectId. |
| POST | /v1/object/copy | ObjectScadService.CopyObject | Copy an object using sourceObjectId or sourceObjectName. |
###### 
###### 
### Get an Object
###### 
* #### `GET /v1/object/get` (REST)
* #### `ObjectScadService.GetObject` (gRPC)

* Its query parameter is also the only field in the gRPC `GetObjectRequest`. 
* For example, `/v1/object/get?objectId=1` retrieves object 1.
###### 
###### 
| Field | Type | Use | Example |
| --- | :---: | --- | --- |
| objectId | integer | Required positive object ID. | ?objectId=1 |
###### 
###### 
### Create or Update a Solid
###### 
* #### `POST /v1/object/solid/createUpdate` (REST)
* #### `ObjectScadService.CreateUpdateSolid` (gRPC)

* Reads a JSON body `CreateUpdateSolidRequest`.
###### 
###### 
| Field | Type | Use | JSON example |
| --- | :---: | --- | --- |
| selectedSolidType | enum | CUBE, ROUND_CUBE, CYLINDER, ROUND_CYLINDER, SPHERE, POLYHEDRON, SURFACE, or TEXT. | "selectedSolidType": "CUBE" |
| objectName | string | Nonempty object name. | "objectName": "Example" |
| description | string | Nonempty solid description. | "description": "Example cube" |
| axisId | integer | Optional axis ID; an axis calling method can be supplied instead. | "axisId": 1 |
| selectedFilament | string | Optional; currently not used by solid creation. | "selectedFilament": "Other" |
| selectedOperationType | enum | UNION, DIFFERENCE, or INTERSECTION. | "selectedOperationType": "UNION" |
| dimensions | object | Solid dimensions and options; see the table below. | "dimensions": { ... } |
| layer | integer | Module layer. | "layer": 0 |
| alpha | number | Optional opacity; defaults to 1. | "alpha": 1 |
| selectedUnit | enum | METRIC or IMPERIAL. | "selectedUnit": "METRIC" |
| decimalPlaces | integer | Optional; defaults to 2. | "decimalPlaces": 2 |
| axisUsedCallingMethod | string | Optional existing axis calling method. | "axisUsedCallingMethod": "axis_name" |
| appendObject | boolean | Optional; defaults to false. | "appendObject": false |
| isPreRendered | boolean | Optional; defaults to true. | "isPreRendered": true |
| colorHex | string | Optional #RGB or #RRGGBB; takes precedence over oscadColor. | "colorHex": "#336699" |
| oscadColor | string | Optional named OpenSCAD color. | "oscadColor": "Silver" |

```json
    {
      // Request Body CreateUpdateSolidRequest
      "selectedSolidType": "CUBE",
      "objectName": "Example",
      "description": "Example cube",
      "axisId": 1,
      "selectedFilament": "Other",
      "selectedOperationType": "UNION",
      "dimensions": {
        "id": 0, "axisDimensionsId": 0,
        "objectModuleId": 0,
        "objectModuleName": "", "solidCallMethod": "",
        "shapeName": "", "material": "",
        "operationType": "UNION", "solidType": "CUBE",
        "description": "",
        "length": 10, "width": 10, "height": 10,
        "thickness": 0,
        "radius": 0, "radius1": 0, "radius2": 0,
        "cylinderHeight": 0,
        "xOffset": 0, "yOffset": 0, "zOffset": 0,
        "xRotate": 0, "yRotate": 0, "zRotate": 0,
        "roundR": 0, "roundH": 0, "resolution": 0,
        "surfaceFilePath": "", "surfaceFileFormat": false,
        "surfaceCenter": false, "surfaceInvert": false,
        "surfaceConvexity": 1,
        "surfaceScaleX": 1, "surfaceScaleY": 1,
        "surfaceScaleZ": 1,
        "textInput": "", "textSize": 10, "textDepth": 0,
        "fontInput": "Sans", "textAlign": "Left",
        "verticalAlign": "Baseline", "textDirection": "L-to-R",
        "languageInput": "EN",
        "volumeCm3": 0, "volumeIn3": 0,
        "oscadMethod": "", "createdAt": ""
      },
      "layer": 0, "alpha": 1,
      "selectedUnit": "METRIC", "decimalPlaces": 2,
      "axisUsedCallingMethod": "axis_name",
      "appendObject": false, "isPreRendered": true,
      "colorHex": "#336699", "oscadColor": "Silver"
    }
```
###### 
###### 
### Delete a Solid or Module
###### 
* #### `DELETE /v1/object/solid/deleteItem` (REST)
* #### `ObjectScadService.DeleteItem` (gRPC)

* Reads a JSON body `DeleteItemRequest`.
###### 
###### 
| Field | Type | Use | JSON example |
| --- | :---: | --- | --- |
| itemType | enum | SOLID or MODULE. | "itemType": "SOLID" |
| itemId | integer | ID of the item to delete. | "itemId": 1 |

```json
    {
      // Request Body DeleteItemRequest
      "itemType": "SOLID",
      "itemId": 1
    }
```
###### 
###### 
### Copy an Object
###### 
* #### `POST /v1/object/copy` (REST)
* #### `ObjectScadService.CopyObject` (gRPC)

* Reads a JSON body `CopyObjectRequest`.
* Omit either source field to select the source by the other. The target name must not already exist. 
* The copy retains the original solid SCAD and rebuilds its object modules under the target name. 
###### 
###### 
| Field | Type | Use | JSON example |
| --- | :---: | --- | --- |
| sourceObjectName | string | Optional source name; use an ID or a name. | "sourceObjectName": "Example" |
| sourceObjectId | integer | Optional source ID; takes precedence over the name. | "sourceObjectId": 1 |
| targetObjectName | string | New name; omitted or empty chooses a name automatically. | "targetObjectName": "New example" |

```json
    {
      // Request Body CopyObjectRequest
      "sourceObjectId": 1,
      "sourceObjectName": "Example",
      "targetObjectName": "New example"
    }
```
###### 
###### 
### Solid Dimensions
###### 
* The `dimensions` object has the following fields. All are optional in the protobuf schema, but requirements depend on the selected solid type. 
* Fields listed as not used are present in the shared message schema but are not consumed by the current solid creation service.
* The complete request template below includes optional and currently unused fields to show the full JSON shape. Replace `axisId` and `axisUsedCallingMethod` with an existing axis; the ID takes precedence. 
* For actual requests, omit unused fields and options for other solid types. The response includes `objectId`, `objectName`, and `objectModule.createdSolid`. Other solid types require different dimensions or options.
###### 
###### 
| Field | Type | Use | JSON example |
| --- | :---: | --- | --- |
| id, axisDimensionsId, objectModuleId | integer | Not used by solid creation. | "id": 0, "axisDimensionsId": 0, "objectModuleId": 0 |
| objectModuleName, solidCallMethod, material | string | Not used by solid creation. | "objectModuleName": "", "solidCallMethod": "", "material": "" |
| shapeName | string | Name of a saved shape for a polyhedron. | "shapeName": "" |
| operationType, solidType | enum | Not used; use the top-level selected types. | "operationType": "UNION", "solidType": "CUBE" |
| description | string | Not used; use the top-level description. | "description": "" |
| length, width, height, thickness | number | cube and round cube require positive length, width, and height. | "length": 10, "width": 10, "height": 10, "thickness": 0 |
| radius, radius1, radius2, cylinderHeight | number | cylinders require positive radius or both radii, and positive height. | "radius": 0, "radius1": 0, "radius2": 0, "cylinderHeight": 0 |
| xOffset, yOffset, zOffset | number | Translation offsets. | "xOffset": 0, "yOffset": 0, "zOffset": 0 |
| xRotate, yRotate, zRotate | number | Rotation values. | "xRotate": 0, "yRotate": 0, "zRotate": 0 |
| roundR, roundH | number | Not used by solid creation. | "roundR": 0, "roundH": 0 |
| resolution | integer | Not used by solid creation. | "resolution": 0 |
| surfaceFilePath | string | File path for a surface. | "surfaceFilePath": "" |
| surfaceFileFormat, surfaceCenter, surfaceInvert | boolean | surfaceFileFormat controls automatic smoothing. | "surfaceFileFormat": false, "surfaceCenter": false, "surfaceInvert": false |
| surfaceConvexity | integer | Surface convexity. | "surfaceConvexity": 1 |
| surfaceScaleX, surfaceScaleY, surfaceScaleZ | number | Surface scaling factors. | "surfaceScaleX": 1, "surfaceScaleY": 1, "surfaceScaleZ": 1 |
| textInput | string | Text for a text solid. | "textInput": "" |
| textSize | number | Text size. | "textSize": 10 |
| textDepth | number | Not used by solid creation; the service uses height. | "textDepth": 0 |
| fontInput, textAlign, verticalAlign, textDirection, languageInput | string | Text font, alignment, direction, and language options. | "fontInput": "Sans", "textAlign": "Left", "verticalAlign": "Baseline", "textDirection": "L-to-R", "languageInput": "EN" |
| volumeCm3, volumeIn3 | number | Not used by solid creation. | "volumeCm3": 0, "volumeIn3": 0 |
| oscadMethod, createdAt | string | Not used by solid creation. | "oscadMethod": "", "createdAt": "" |
###### 
###### 