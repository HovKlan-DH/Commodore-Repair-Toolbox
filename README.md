# Commodore Repair Toolbox

This is the official home page for _Commodore Repair Toolbox_ (named as _CRT_ hence forward) - a Windows utility tool for repairing vintage computers or peripherals.

> [!CAUTION]
> Please do note that the data files have not yet reached its final maturity level, and by then its format _could_ change in a nearby future release!

# Screenshots

Main screen:\
![Main screen](https://github.com/user-attachments/assets/e2e22044-d2d2-4b57-9931-7357919d8897)

Popup component info:\
![Component popup information](https://github.com/user-attachments/assets/087dda87-c368-4dac-be0c-564916d70318)

# What is it?

With _CRT_ you can easily view schematics, zoom in/out, identify components, see chip pinouts, study datasheets, ressources and various other information, helping you diagnosing and repairing good old vintage hardware. It is primarily dedicated to Commodore, and have a few built-in configurations for Commodore computers, but it does support any kind of hardware, as you can add your own data - e.g. other computers, radios, DIY electronics or whatever else you can imagine.

It probably works the best, if the hardware is "simple" and have good documentation available, like schematics, and if it is something you need to revisit multiple times - then you can add the needed information yourself, and use it for easy future reference.

# Installation and usage

_CRT_ does not require any installation - just download the newest ZIP file from [Releases](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/releases), extract it and run the executable file, `Commodore-Repair-Toolbox.exe`

It will run on any 64-bit Windows 10 or newer. It does require _.NET Framework 4.8.1_, but this is an integrated part of the operating system.

# Built-in hardware / schematics

- **Commodore 64 (Breadbin)**
  - 250407
  - 250425
- **Commodore 64C**
  - 250466
- **Commodore 128 and 128D**
  - 310378

# Your help would be really appreciated

I would really appreciate if some Commodore experts would take a look, and let me know of obvious factual errors or things I have missed :pray:

You can also help specifically with these topics:
- Do you have higher-quality images of the schematics?
- Do you have (better) datasheets or pinouts for any of the components?
- Do you see missing components in either the component list or as a highlight?
- Can you improve any data or fill in more technical details anywhere?

You can also contribute by adding new data and send the Excel file and "label file" to me (view below for the software used for labeling).

# Contact
You can create a new [Issue](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/issues) here in the GitHub project page or use the built-in "Feedback" tab, if you want to report a problem, suggest any changes or just have a comment.

# Documentation
Please refer to The [Wiki Documentation](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/wiki/Documentation) page.
For labelling the components in the schematics  I have discovered the Windows open source application [VGG Image Annotator](https://www.robots.ox.ac.uk/~vgg/software/via/) version 2.0.12. It is quite handy and 

# Future ideas / wishlist

There are major changes in some of these, and I am not sure I want to go down this road - it will for sure depend on usage, as I personally do not have any use for the collaboration topics, if this is primarily used only by myself :grin:

- More pictures in popup component info (data model is ready for it)
- Have people download and upload new schematics or updates directly from tool
    - "Data has been updated" information
    - Rating system of user uploaded schematics

# Data sources used for configurations

I have taken data from many places, but I do have a few _go-to_ places I frequently visit:
- [My Old Computer](https://myoldcomputer.nl/technical-info/mainboards/) (view below also)
- [Zimmers](https://www.zimmers.net/anonftp/pub/cbm/schematics/computers/)
- [Ray Carlsen](https://portcommodore.com/rcarlsen/cbm/)

In some cases I provided or modified the data myself, which is why some expert facts-check would be very much appreciated.

If you want to contribute with a major new or updated dataset, then please let me know, as I happily will show credits here for joint efforts. There is a large Commodore community, and I wish for collaboration into making this a great reference for Commodore reparing (well, on the Windows platform at least).

# Inspiration for creating this project

I have been repairing Commodore 64/128 for some years, but I still consider myself as a _beginner_ in this world of hardware (I am more a software person). I often forget where and what to check, and I struggle to find again all the relevant ressources and schematics to check, not to mention how to find the components in the schematics. I did often refer to the "Mainboards" section of [My Old Computer](https://myoldcomputer.nl/technical-info/mainboards/), and I noticed that Jeroen did have a prototype of an application named _Repair Help_, and it did have the easy layout I was looking for (I did get a copy of it). However, it was never finalized from him, so I took upon myself to create something similar, and a couple of years later I did come up with a very similar looking application, also utilizing a lot of data directly from Jeroen's page (with permission from him, of course).

For now I will continue to add more and better data for myself, when doing my diagnostics and repairing of Commodore equipment, and then I can only hope that others will take the initiative and time it takes to add additional hardware - then I can have this data available somehow at the project home page.
