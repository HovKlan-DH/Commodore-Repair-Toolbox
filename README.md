# Commodore Repair Toolbox

This is the official home page for _Commodore Repair Toolbox_ (named as _CRT_ hence forward) - a Windows helper tool for repairing vintage computers or peripherals.

# Screenshots

![Main screen](https://github.com/user-attachments/assets/6baf7728-b9a0-4242-ae21-c0e6a64c7120)

![iPopup info](https://github.com/user-attachments/assets/4950cf74-7c5f-4a6f-89fd-4a698301c64d)

# Can you explain what it is?

With _CRT_ you can easily view schematics (zoom in/out), identify components, see chip pinouts, study datasheets, ressources and various other information, helping you diagnosing and repairing good old vintage hardware. It is primarily dedicated to Commodore, and have a few _built-in_ configurations, but it does support any kind of hardware, where you can add your own data - e.g. other computers, radios, DIY electronics or whatever else you can imagine. It probably works the best, if the hardware is "simple" and have good documentation available, and if it is something you need to revisit multiple times - then you can add the needed information yourself, and use it for easy future reference.

# Installation and usage

_CRT_ does not require any installation - just download the newest ZIP file from [Releases](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/releases), extract it and run the executable file.\
It does require _Windows .NET Framework 4.8.1_, but this should be part of any Windows Operating System by default.

# Your help would be really appreciated

I really would appreciate if some experienced Commodore repair-gurus would take a look, and let me know of factual errors or things I have missed.\
Also, if you have higher quality images of the schematics then please let me know, as the original schematics sometimes can be impossible to interpret due to handwriting or poor scanning quality.\
You can help adding additional data or enhancing existing, and send the Excel and "label files" to me (view below for software used for labelling).

You can create a new [Issue](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/issues) in the project or write an email to **crt** at **mailscan** dot **dk** if you have any changes or comments.

# Software used
For labelling I have discovered the Windows open source application [VGG Image Annotator](https://www.robots.ox.ac.uk/~vgg/software/via/) version 2.0.12. It is quite handy and easy to use, once you learn its way of working. It will output a JSON file, which I then convert into an Excel format - this JSON file + the image file used I both need, if you have any updates.

I have chosen Excel as the data format, as this is easy for the most to navigate in.

# Nearby TODO

- Fix clicking in thumbnail cannot activate image, if clicked at component location
- Fix "Ressources" tab should be taken from Excel file instead via web
- Change component list is a little easier to look at (remove questionmark)
- Show asterisk (*) in thumbnail label, when chosen component is visible in thumbnail image
- Label in thumnail should not float above image (show label first, then image below)
- Add more data for Commodore 128 (datasheets and pinouts)
- Add data for Commodore 64 (Breadbin) schematics 250407 and 250425
- Add data for Commodore 64C schematics 250466

# Roadmap and ideas

For now you should probably see this more as _ideas_ and _wishes_, as nothing has been set in stone yet. Some of it will be done for sure, where other parts are bigger changes, and I am not sure I want to go down that road - depends very much on usage.

- Configuration file - refactor it and save more configuration:
  - Selected component categories per board
  - Start in same size as last (maximized or window)
- Ideally make fullscreen mode less "flickering"
- "New version available" or "Data has been updated" information
- Have people download and upload new schematics or updates directly from tool
- Rating system of user uploaded schematics

# Inspiration for creating this project

I have been repairing Commodore 64/128 for some years, but I am by far still a "noob" for this. I often forget where and what to check, so I find it hard to find again all the relevant ressources and schematics to check, not to mention how to find the components in the schematics. I did often refer to the "Mainboards" section of [My Old Computer](https://myoldcomputer.nl/technical-info/mainboards/), and I noticed that Jeroen did have a prototype of an aapplication named _Repair Help_, and it did have the easy layout I was looking for (I did get a copy of it). However, it was never finalized, so I took upon myself to create something similar, and a couple of years later I did come up with a very similar looking application, also utilizing a lot of data directly from Jeroen's page, [My Old Computer](https://myoldcomputer.nl/) (with permission from him, of course).

For now I will continue to add more and better data for myself, when doing my diagnostics and repairing of Commodore equipment, and then I can only hope that others will take the initiative and time it takes to add additional hardware - then I can have this data available somehow at the project home page.
