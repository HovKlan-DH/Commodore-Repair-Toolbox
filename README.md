# Commodore Repair Toolbox

This is the official home page for _Commodore Repair Toolbox_ (named as _CRT_ hence forward) - a Windows utility tool for repairing vintage computers or peripherals.

> [!CAUTION]
> Please do note that the data files have not yet reached its final maturity level, and by then its format _could_ change in a nearby future release! Only relevant if you do your own data modification or use an older version.


# Screenshots

**Commodore 64**, assy **250425** schematic view with a couple of circuit traces:\
<img width="1202" height="721" alt="image" src="https://github.com/user-attachments/assets/67ec34ea-b4bf-4d79-b4c7-645c21a470d1" />

**Commodore 128**, **U21** component information popup:\
<img width="902" height="756" alt="image" src="https://github.com/user-attachments/assets/dff7e7d5-c509-425b-acd2-3d9e9a9b95b6" />

**Commodore 128**, **U21** oscilloscope baseline measurement:\
<img width="902" height="756" alt="image" src="https://github.com/user-attachments/assets/07e9c66f-8c2f-4155-8ba9-87911b467a50" />


# What is it?

With _CRT_ you can easily view schematics, zoom in/out, identify components, see chip pinouts, do manual circuit tracing, study datasheets, view oscilloscope readings, ressources and various other information, helping you diagnosing and repairing old vintage hardware. For some parts it supports specific views for _PAL_ and _NTSC_ region.

It is primarily dedicated to Commodore, and have a few built-in datasets for Commodore computers, but it does support any kind of hardware, as you can add your own data - e.g. other computers, radios, DIY electronics or whatever else you can imagine. It probably works the best, if the hardware is "simple" and have good documentation available, like schematics, and if it is something you need to revisit multiple times - then you can add the needed information yourself, and use it for easy future reference.


# Requirements

* Windows 10 (64-bit) or newer
* .NET Framework 4.8.1 (integrated part of the operating system)

This will _not_ work in Linux with Wine, Mono or alike.


# Installation and usage

_CRT_ does not require any installation - just download the newest ZIP file from [Releases](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/releases), extract it and run the executable file, `Commodore-Repair-Toolbox.exe`

When launched _CRT_ you can download newer data (if present) from its online source.


# Built-in hardware / boards

- **Commodore VIC-20**
  - 250403 (CR)
- **Commodore 64**
  - 250407
  - 250425
  - 250466
  - 250469
- **Commodore 128 and 128D** (C128D plastic model, not DCR)
  - 310378
    - Has full oscilloscope baseline readings for a PAL system


# Your help would be really appreciated

I would really appreciate if some seasoned Commodore experts would take a brief look, and let me know of obvious factual errors or things I have missed, as ideally this tool could be used by many as a helper-tool, keeping our beloved Commodore hardware running :pray:

You can also help specifically with these topics:
- Do you have higher-quality images of the used schematics?
- Do you have (better) datasheets or pinouts for any of the components?
- Do you see missing components in either the component list or as a highlight?
- Can you improve any data or fill in more technical details anywhere?
- I have done some oscilloscope baseline measurements on a good working **PAL** system - would you be able to assist with the same reading on a **NTSC** system?
  - Please connect with me, so we can coordinate on _what_ and _how_ to measure.

You can also contribute by adding new data and send the Excel file and "label file" to me (view [Wiki Documentation](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/wiki/Documentation) for how to use the labeling software).


# Contact

You can create a new [Issue](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/issues) here in the GitHub project page or use the built-in "Feedback" tab, if you want to report a problem, suggest any changes or just have a comment. I would appreciate some feedback, to know if this is actually useful for others or if you see some missing functionalities that could be benefitial for many.


# Documentation of data

Please refer to the [Wiki Documentation](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/wiki/Documentation) page for adding or modifying the data.


# FAQ

* **Will it work on Linux with Wine or Mono?**
  - Short answer is "_no_".
  - Due to various reasons _CRT_ uses a newer _WebView2_ component, which gives all kind of issues when not running on a native Windows 10 or newer. It can start on Linux, but it then comes with a ton of warnings, which does make it very troublesome to work with.
* **Will it work on Windows 7?**
  - No - same reason as with Linux.
  - The _WebView2_ component requires a certain newer .NET Framework version, which cannot run on Windows 7.
* **Why not creating this as a web page instead?**
  - Because I like the responsiveness and feel of a real native Windows application :-)
  - A web page would be for someone else to create, as that would be very heavy on the client-side, and this is not my favorite way of coding.
* **Will you port this to Windows or Linux?**
  - Not any day soon. Maybe one day when AI becomes better at handling complex tasks, then I could look in to this, but here-and-now I will only focus on the Windows application as-is.


# Data sources used for configurations

I have taken data from many places, but I do have a few _go-to_ places I frequently visit:
- [My Old Computer](https://myoldcomputer.nl/technical-info/mainboards/) (view below also)
- [Zimmers](https://www.zimmers.net/anonftp/pub/cbm/schematics/computers/)
- [Ray Carlsen](https://portcommodore.com/rcarlsen/cbm/)

In some cases I provided or modified the data myself, which is why some expert facts-check would be very much appreciated.

If you want to contribute with a major new or updated dataset, then please let me know, as I happily will show credits here for joint efforts. There is a large Commodore community, and I wish for collaboration into making this a great reference for Commodore reparing (well, on the Windows platform at least).


# Inspiration for creating this project

I have been repairing Commodore 64/128 for some years, but I for sure still consider myself as a _beginner_ in this world of hardware (I am more a software person). I often forget _where_ and _what_ to check, and I struggle to find again all the relevant ressources and schematics to check, not to mention how to find the components in the schematics. I did often refer to the "Mainboards" section of [My Old Computer](https://myoldcomputer.nl/technical-info/mainboards/), and I noticed that Jeroen did have a prototype of an application named _Repair Help_, and it did have the easy layout I was looking for (I did get a copy of it). However, it was never finalized from him, so I took upon myself to create something similar, and a couple of years later I did come up with a very similar looking application, also utilizing some data directly from Jeroen's page (with permission from him, of course).

For now I will continue to add new and refine data for myself, when doing my diagnostics and repairing of Commodore equipment, and then I can only hope that others will take the initiative and time it takes to add additional hardware or help refine the existing data - then I can have this data available somehow at the project home page.
