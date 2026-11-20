 # <img src="NetGenCAD.UI/NetGenCAD.UI/Assets/Images/logo.png" height="30" width="30"> NetGenCAD
###### 
#### Description
This application is a no-code tool for generating custom objects used in OpenSCAD, a solid 3D CAD modeler. **It is assumed that users have a basic understanding of OpenSCAD and its functionalities.** 
###### 
#### Prerequisites
* **You need a CAD renderer installed (OpenSCAD recommended):** [OpenSCAD](https://openscad.org/downloads.html)
* [NetGenCAD for Windows (x64)](NetGenCAD.UI/NetGenCAD.UI.Windows/NetGenCAD-0.0.6-winx64.7z)
* **NetGenCAD for Linux (x64 & arm64)** Coming Soon!
* **NetGenCAD for MacOS (x64 & arm64)** Coming Soon!

###### 
#### Types of Solids Available
* **Cube** - default aligned on 0,0,0 axes. Can be offset with Adjust X, Y, Z values.
* **Cylinder** - all cylinders are aligned on 0,0,0 axes, with center point at center of circle.
* **Polyhedron** - created in Shape Designer, and multiple polyhedrons can be used within Object Designer.
* **Rounded Cube** - Minkowski rounding using offsets to align to 0,0,0 axes
* **Rounded Cylinder** - Minkowski rounding using offsets to align to 0,0,0 axes. Center is center of circle.
* **Sphere** - all spheres are aligned on the 0,0,0 axes, with center point of the sphere at 0,0,0.
* **Surface** - import from png or dat file. Default aligned on 0,0,0 axes or can be offset with translate.
* **Text** - create 3D text using OpenSCAD text function with approximate bounding box dimensions.

###### 
#### Examples of Objects Created
###### 
![Bullion](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/bullionBars.png)
![Bullion Create Dark](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/bullionBarsCreateDark.png)

###### 
![Coin](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/coin.png)
![Coin Create Dark](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/coinCreateDark.png)

###### 
![Coil Create Dark](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/dataPyramid.png)
![Polyhedron Inputs Dark](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/polyhedronInputsDark.png)

###### 
![Coil](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/redAlertTeslaCoil.png)
![Coil Create Dark](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/coilCreateDark.png)

###### 
#### Solid Adjustments Available
* **Rotate** - rotate along X, Y, and/or Z axis when creating/updating a solid
* **Scale** - scale along X, Y, and/or Z axis when importing an image or dat file
* **Translate** - place the solid at specified position, along X, Y, and/or Z axis
* **Color** - apply color to the solid being created/updated
* **Alpha** - apply transparency to the solid being created/updated
* **Layer** - exclusive NetGenCAD feature for specifying solid difference or intersections

###### 
#### Object Adjustments Available
* **Mirror** - mirror along X, Y, and/or Z axis when exporting or viewing the object
* **Copy** - create duplicates of the object being created/updated
* **Render** - applies optimized rendering of solids before viewing or exporting the object
* **Export** - export the object as an STL file for 3D printing or other uses

###### 
#### Layout of Custom Axes Builder
The Custom Axes Builder has 2 main visual sections: Custom Axis and Generated Axes. The outputs of newly created axes will show up in the tables next to the Custom Axis section.

Create or select an axis and choose **View Output** to open its preview in the default SCAD application. **Preview Color** overrides the axis color only in the preview; **Clear Inputs** restores the axis default. The preview origin choices reflect the axis within its X and Y bounds; they do not change the saved axis or the OpenSCAD camera. Changes to the preview update its file, which may require reloading in the SCAD application. Mirroring also reverses the axis labels.

Choose a mirrored origin and select **Save Mirror** to create a separate axis with its own calling method and file. After saving, the inputs and temporary mirror controls reset, the axes list refreshes, and the preview file is rewritten to call the saved axis without additional top-level transforms. The preview opens if it was not already open. **Mirror Z** independently flips the axis within its Z bounds and can be combined with any corner. It reflects the geometry, not the OpenSCAD camera, and reverses the labels. The saved axis appears in the generated axes list and can be used or deleted independently of the original. Its calling method encodes the millimeter translation and reflected axes, for example `_Orig_300_x_0_x_0_Mirror_X_0_0`. Negative signs are encoded as `N` and decimal points as `D`. Saving the same variant again updates it instead of creating a duplicate.
###### 
![Axis Creation](NetGenCAD.UI/NetGenCAD.UI/Assets/Images/axisCreation.png)
###### 
#### Types of Measurements
Varying axes of different sizes, measurement types, colors, and combinations of both metric and imperial measurements can be applied within the same SCAD project. Axes are managed in an aggregate SCAD file and are called as modules within your SCAD project. The app manages updates to existing axes as well as provides the ability for multiple axis types to be used in your project. Axis increments are as follows:
###### 
* **Metric axis** - 20mm, 10mm, 5mm, and 1mm increments
* **Imperial axis** - 1/4", 1/8", 1/16", and 1/32" increments
* **Convert mm to inches** - Enter inputs in Metric (mm) first, then select Imperial (in)
* **Convert inches to mm** - Enter inputs in Imperial (in) first, then select Metric (mm)

Each object and its solids share one axis reference. Changing or clearing a solid's axis updates the object and its other solids; a new solid without an axis inherits the object's axis. When existing data is migrated, the newest solid's axis is used for its object (creation timestamp, then ID as a tiebreaker). Missing referenced axes must be corrected before migration.

###### 
#### Generated SCAD Files

Code can remove a generated axis with `GUI.RemoveAxis(outputDirectory, callingMethod)`, using the calling method returned by `GUI.SetAxis` or retrieved from the database. It removes matching generated entries from `axes.scad` and deletes their included axis file; it does not change database records. Missing, malformed, or shared entries are reported rather than deleting a potentially unrelated file. If file deletion fails after the index is updated, the error is propagated and the axis file may need to be removed separately.

| Files                                   | Usage In Your SCAD File / Description                  | Optional parameters | Render |
| --------------------------------------- |:------------------------------------------------------:|:-------------------:|:------:|
| Scad/Axes/axes.scad                     | Description: holds all stored axes used in 3D models   |                     |	No    |
| Scad/Axes/custom_axis_name.scad         | Syntax: use <Axes/axes.scad>; Get_Custom_Axis_Name();  | colorVal, alpha     |	No    |
| Scad/Solids/moduleType_name_object.scad | Syntax: include <object_name_type.scad>;               |                     |	Yes   |
| Scad/Solids/object.scad                 | Description: main object file for 3D model             |                     |	No    |	
| Scad/Solids/object.stl                  | Description: exported STL file for 3D model            |                     |  Yes   |
| Scad/Solids/polyhedronName_shape.scad   | Description: polyhedron shape preview file             |                     |	No    |
