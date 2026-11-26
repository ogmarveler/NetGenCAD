# Local API Guide

The API is available while NetGenCAD is running at `http://localhost:5000`. The top-bar list also shows the Windows named pipe or Linux Unix socket. The indicator checks the localhost HTTP server only, regardless of which address is selected; it does not check the pipe, socket, every API operation, or the database. Green means the server responded, and red means it is starting, stopped, or unreachable. Select the indicator to check again; it also checks automatically every five seconds. Hover over it for details.

Send JSON request bodies with `Content-Type: application/json`. Protobuf JSON field names can be written in camelCase or snake_case; enum names such as `METRIC` and `CUBE` or their numeric values are accepted. Responses use protobuf JSON, including enum names and string values for 64-bit integers. Errors return an HTTP status and a JSON object with `code`, `message`, and `details`.

## Axis endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/v1/axes` | List metric and imperial axes. |
| GET | `/v1/axes/convertUnits` | Convert between metric and imperial values. |
| POST | `/v1/axes/createUpdate` | Create or update an axis. |
| DELETE | `/v1/axes/deleteAxis` | Delete an axis by `axisId` or `axisCallingMethod`. |

`GET /v1/axes` returns metric and imperial lists without the verbose `axisOscad` and `axesOscad` fields. Use `GET /v1/axes?axisId=1` (or `axis_id=1`) to return only that axis, including its OpenSCAD definitions. An unknown ID returns HTTP 404; an invalid ID returns HTTP 400.

Example conversion request:

```json
{"maxX":25.4,"decimalPlaces":2,"fromUnit":"METRIC"}
```

Send this JSON body with GET to `/v1/axes/convertUnits` to convert 25.4 mm to 1 inch. Set `fromUnit` to `IMPERIAL` for the reverse conversion. Some HTTP clients do not support bodies on GET requests. Conversion also accepts `minX`, `minY`, `maxY`, `minZ`, `maxZ`, `volume`, and `volumeScale`.

Example axis creation request for `/v1/axes/createUpdate`:

```json
{
  "minX": 0, "maxX": 100,
  "minY": 0, "maxY": 100,
  "minZ": 0, "maxZ": 100,
  "selectedUnit": "METRIC",
  "selectedBackground": "LIGHT",
  "decimalPlaces": 3,
  "colorHex": "#336699",
  "axisColorAlpha": 1
}
```

Each minimum must be at most zero and less than its maximum; each maximum must be at least zero. Units are `METRIC` or `IMPERIAL` and backgrounds are `LIGHT` or `DARK`. `decimalPlaces`, `colorHex`, `oscadColor`, and `axisColorAlpha` are optional; `colorHex` takes precedence over `oscadColor`. The response includes `axisId` and `callingMethod`. To delete that axis, send a DELETE body such as `{"axisId": 1}` to `/v1/axes/deleteAxis`.

## Object endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| POST | `/v1/object/solid/createUpdate` | Create or update a solid. |
| DELETE | `/v1/object/solid/deleteItem` | Delete a solid or module using `itemType` and `itemId`. |
| POST | `/v1/object/copy` | Copy an object using `sourceObjectId` or `sourceObjectName`. |
| POST | `/v1/object/createUpdate` | Declared but not implemented; returns HTTP 501. |

Example solid creation request for `/v1/object/solid/createUpdate` (replace `axisId` with an existing axis ID):

```json
{
  "selectedSolidType": "CUBE",
  "objectName": "Example",
  "description": "Example cube",
  "axisId": 1,
  "selectedOperationType": "UNION",
  "selectedUnit": "METRIC",
  "dimensions": { "length": 10, "width": 10, "height": 10 }
}
```

Cube dimensions must be positive. Other solid types include `ROUND_CUBE`, `CYLINDER`, `ROUND_CYLINDER`, `SPHERE`, `POLYHEDRON`, `SURFACE`, and `TEXT`; their required dimensions differ. Optional request fields include `layer`, `alpha`, `decimalPlaces`, `colorHex`, `oscadColor`, and the nested dimension and transform fields. The response includes `objectId`, `objectName`, and `objectModule.createdSolid`.

To delete a solid or module, send a DELETE body such as `{"itemType":"SOLID","itemId":1}` to `/v1/object/solid/deleteItem`. To copy an object, POST `{"sourceObjectId":1,"targetObjectName":"New example"}` or `{"sourceObjectName":"Example","targetObjectName":"New example"}` to `/v1/object/copy`. If both source fields are present, the ID takes precedence. The target name must not already exist. If `targetObjectName` is omitted or empty, the service chooses a copy name automatically. The copy retains the original solid SCAD and rebuilds its object modules under the target name. Each layer containing difference or intersection solids requires a union solid; object modules must have matching solids for rebuilding.

The full request and response fields are defined in `createAxes.proto` and `objectScad.proto` in the source repository. Shape routes use a separate transcoder and may not work in an AOT build.

The health indicator does not start or restart the API. If it stays red, check whether another application is using port 5000 and inspect the app's error output. A successful health check does not guarantee that an individual API call will succeed.
