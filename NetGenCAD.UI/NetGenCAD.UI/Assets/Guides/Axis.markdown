 
# ![Logo](Assets/Images/logo.png) NetGenCAD Custom Axis Guide
###### 
#### Description
* Welcome to the NetGenCAD Axis Guide! This tutorial covers the basics of defining and generating custom axes used in OpenSCAD, a solid 3D CAD modeler.
###### 
#### Types of Measurements
* Varying axes of different sizes, measurement types, colors, and combinations of both metric and imperial measurements can be applied within the same SCAD project.
* Axes are managed in an aggregate SCAD file and are called as modules within your SCAD project. 
* **Metric axis** - 20mm, 10mm, 5mm, and 1mm increments
* **Imperial axis** - 1/4", 1/8", 1/16", and 1/32" increments
* **Convert mm to inches** - Enter inputs in Metric (mm) first, then select Imperial (in)
* **Convert inches to mm** - Enter inputs in Imperial (in) first, then select Metric (mm)
###### 
#### Layout of Custom Axes Builder
* The Custom Axes Builder has 2 main visual sections: Custom Axis and Generated Axes. The outputs of newly created axes will show up below the Custom Axis section. 
* This shows the total cubic size, as well as the name to use in your project file. 
* In the list below the custom axis settings, you will see the newly created axis, along with axes that have been previously created. 
* There are two tables that display axes information. One is metric axes and the other is imperial axes. **Both ascending and descending sorting are possible, as well as sorting by multiple categories (shift + select).**
###### 
#### Usage of Custom Axes in OpenSCAD
###### 
* **Axes Module Name Format:** Get_ + { Theme } + { X Range } + { Y Range } + { Z Range } + { Unit Scale } + Origin + { Min X } + { Min Y } + { Min Z } (N = negative, i.e., N1 is -1)
* **Mirrored Calling Method Suffix:** {Axis Module Name Format} + Mirror_ + {X|0} + {Y|0} + {Z|0} 
* X, Y, or Z indicates that axis is mirrored on this specific axis; 0 means it is not. 
* For example, Get_Light_10x10x10_MM_Orig_0x0x0_**Mirror_X_0_0()**; mirrors X only. 
* Calling methods accept optional (colorVal, alpha) parameters, e.g. Get_Light_10x10x10_MM_Orig_0x0x0_Mirror_X_0_0(colorVal = "Red", alpha = 0.5);.
###### 
#### Additional Notes
* Axes are stored in a **Scad/Axes** folder that is included with the application. 
* **The axes.scad and individual axis SCAD files within the Axes folder are NOT designed to be edited**, as they are managed by the application itself. 
* Please understand if editing these files, it could break functionality of use of these files within the application itself.
###### 
###### 
![AxisCreation](Assets/Images/axisCreationNew.png)
###### 
###### 
## Setting Inputs for New Axis
###### 
| Steps                                                                         | Requirements / Options                  | Default Value |
| ----------------------------------------------------------------------------- |:---------------------------------------:|:-------------:|
| 1. Open the **Axes** menu and select **Create New Axes**                      |                                         |               |
| 2. Select the **Unit Type**                                                   | Metric (mm) or Imperial (in)            | Metric (mm)   |
| 3. Select the **OpenSCAD** theme used in OpenSCAD                             | Light Theme or Dark Theme               | Light Theme   |
| 4. Enter numeric **Min X** value                                              | Min X **<=** 0 and Min X **<** Max X    | 0 (mm or in)  |
| 5. Enter numeric **Max X** value                                              | Max X **>=** 0 and Max X **>** Min X    | 300mm or 12"  |
| 6. Enter numeric **Min Y** value                                              | Min Y **<=** 0 and Min Y **<** Max Y    | 0 (mm or in)  |
| 7. Enter numeric **Max Y** value                                              | Max Y **>=** 0 and Max Y **>** Min Y    | 300mm or 12"  |
| 8. Enter numeric **Min Z** value                                              | Min Z **<=** 0 and Min Z **<** Max Z    | 0 (mm or in)  |
| 9. Enter numeric **Max Z** value                                              | Max Z **>=** 0 and Max Z **>** Min Z    | 300mm or 12"  |
| 10. Optional: Select **Hex** to enable hexadecimal color input                | Enter a 3- or 6-digit hex value (#RGB or #RRGGBB) | Off           |
| 11. Optional: Select a **Color**                                              | OpenSCAD colors or the hex value        | Theme-based   |
| 12. Optional: Enter numeric **Alpha** for color opacity                       | 0 - 1 (values below 0.01 display as 0.01) | 1           |
| 13. Click the **Create Axis** button to generate the defined axis             |                                         |               |
| 14. To revert to default settings, click the **Clear** button                 |                                         |               |
| 15. To preview the axis and/or colors, **click on the Axis in the table**     | Optional params: (colorVal, alpha)      | Theme-based   |
| 16. Call the new axis using the **Calling Method**                            | Optional params: (colorVal, alpha)      | Theme-based   |
| 17. To view output files, open the **Scad/Axes folder** in the app's directory|                                         |               |
###### 
###### 
## Setting Inputs for Mirroring an Axis
###### 
| Steps                                                                         | Requirements / Options                  | Default Value |
| ----------------------------------------------------------------------------- |:---------------------------------------:|:-------------:|
| 1. Select the original axis in the generated axes table                       | Create or select an axis first          |               |
| 2. Open the axis preview using **View Output**                                |                                         |               |
| 3. Choose a mirrored corner: **Top Right** for X, **Top Left** for X and Y, or **Bot Left** for Y | Bot Right keeps the original orientation | Bot Right |
| 4. Optional: Select **Mirror ?** to flip the axis along Z                     | Can be combined with any mirrored corner | Off          |
| 5. Optional: Choose a preview **Color** and/or **Alpha**                      | These become the saved mirror's defaults; no color uses the original axis color | Original axis settings |
| 6. Click **Save Mirror** to create a separate axis                            | At least one X, Y, or Z reflection is required |               |
| 7. To create another orientation, select the original axis again and repeat   | The saved mirror appears in the generated axes table |               |
###### 
###### 
## Imperial and Metrix Axes Tables Actions
###### 
| Actions                                                                       | Requirements / Options                  | Default Value |
| ----------------------------------------------------------------------------- |:---------------------------------------:|:-------------:|
| 1. **Trash Bin Icon** - deletes a selected axis from the axes.scad file and DB | Imperial or Metrix Axes Table          |               |
| 2. **Clipboard Icon** - opens a modal showing the calling method to use       | Imperial or Metrix Axes Table           |               |
| 3. **Sorting** - click on one or more column headers to sort (shift + click)  |       Imperial or Metrix Axes Table     |               |
###### 
###### 
## Generated SCAD Files
###### 
| Files                                   | Usage In Your SCAD File / Description                  | Optional parameters | Render |
| --------------------------------------- |:------------------------------------------------------:|:-------------------:|:------:|
| Scad/Axes/axes.scad                     | Description: holds all stored axes used in 3D models   |                     |	No    |
| Scad/Axes/axis_preview.scad             | Description: can preview a newly created axis and mirror | colorVal, alpha |	No    |
| Scad/Axes/custom_axis_name.scad         | Syntax: use <Axes/axes.scad>; Get_Custom_Axis_Name();  | colorVal, alpha     |	No    |