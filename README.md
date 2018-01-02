Create and move a cube, a sphere or a [monkey](https://en.wikipedia.org/wiki/Blender_(software)#Suzanne) in your browser using WebGL (based on [three.js](https://github.com/mrdoob/three.js/)) and it moves in the Unity Player, Unity Editor or Windows Standalone and UWP (including Hololens or Mixed Reality Hardware).

This screenshot shows the Web-Version. Just start the Server with the `meteor` command. When it is loaded you can create new objects using the buttons in the top-right. Click the object to show the manipulation tool. With this tool you can use the arrows to position the object.
![Screenshot](move_geometry_objects_screenshot.png?raw=true "Screenshot showing the Web-Version")

Note that when you move it it will also move in your Application or Unity Editor. The Editor needs to have the focus to update the viewport. So if you don't see any changes click the Editor or App.

(TODO: video here)

This is an example application for the [Unity 3D DDP client](https://github.com/green-coder/unity3d-ddp-client/tree/dev) (dev branch).

> Note that this needs Unity 2017 or newer for .Net 4.6 

> Tested with Unity 2017.3.0f3, Windows SDK 10.0.16299.0, Visual Studio 2017 in the Unity Editor, on a Windows PC and on HoloLens.
