# Commodore Repair Toolbox

This is the official home page for _Commodore Repair Toolbox_ (named as _CRT_ hence forward) - a Windows* utility tool for repairing vintage computers or peripherals.

*Note: As of version 2026-February-12 it _should_ be able to run in Linux with Wine and Mono, but this is very much in experimental mode.


# What is it?

With _CRT_ you can easily view technical schematics, zoom, identify components, view chip pinouts, do manual circuit tracing, study datasheets, view oscilloscope images, ressources and various other information, helping you diagnosing and repairing old vintage hardware.

It is primarily dedicated to Commodore, and have a few built-in profiles for Commodore computers, but it can support any kind of hardware, as you can add your own data - e.g. other computers, radios, DIY electronics or whatever else you can imagine. It probably works the best, if the hardware is "simple" and have good documentation available, like schematics, and if it is something you need to revisit multiple times - then you can add the needed information yourself, and use it for easy future reference.

Almost all Commodore boards have a full oscilloscope baseline for the PAL and NTSC regions, making it a great reference for how a working setup looks like.


# Screenshots

**Commodore 64**, assy **250425** schematic view with a couple of circuit traces:\
<img width="1202" height="721" alt="image" src="https://github.com/user-attachments/assets/67ec34ea-b4bf-4d79-b4c7-645c21a470d1" />

**Commodore 128**, **U21** component information popup:\
<img width="902" height="756" alt="image" src="https://github.com/user-attachments/assets/dff7e7d5-c509-425b-acd2-3d9e9a9b95b6" />

**Commodore 128**, **U21** oscilloscope baseline measurement:\
<img width="902" height="756" alt="image" src="https://github.com/user-attachments/assets/07e9c66f-8c2f-4155-8ba9-87911b467a50" />


# Requirements

* Windows 7 or newer (works with both 32/64-bit)
* .NET Framework 4.8 or newer

As per version **2026-February-12** there is **experimental** support for Linux with Wine and Mono installed... but it is not yet officially supported, though I would like to know if it works or not.


# Installation and usage

_CRT_ does not require any installation - just download the newest ZIP file from [Releases](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/releases), extract it and run the executable file, `Commodore-Repair-Toolbox.exe`, The ZIP file contains a full set of data.

When launched _CRT_ you can download newer data (if present) from its online source.


# Built-in hardware and boards

- **Commodore VIC-20**
  - 250403 (CR)
- **Commodore 64**
  - 250407 (long board)
    - Covers _all_ components
    - Oscilloscope baseline measurements for PAL and NTSC
  - 250425 (long board)
    - Covers _all_ components
    - Oscilloscope baseline measurements for PAL and NTSC
  - 250466 (long board)
    - Covers _all_ components
    - Oscilloscope baseline measurements for PAL and NTSC
  - 250469 (short board)
    - Covers _all_ components
    - Oscilloscope baseline measurements for PAL and NTSC
- **Commodore 128 and 128D** 
  - 310378 (C128 and C128D, plastic cabinet)
    - Covers _all_ components
    - Oscilloscope baseline measurements for PAL and NTSC
  - 250477 (C128DCR, metal cabinet)
    - Covers _all_ components


# Your help would be really appreciated

I would really appreciate if some seasoned Commodore experts would take a look, and let me know of obvious factual errors or things I have missed, as ideally this tool could be used by many as a reference- and helper-tool, keeping our beloved Commodore hardware running :pray:

You can also help specifically with these topics:
- Do you have higher-quality images of the used schematics?
- Do you have (better) datasheets or pinouts for any of the components?
- Do you see missing components in either the component list or as a highlight?
- Can you improve any data or fill in more technical details anywhere?

You can also contribute by adding new data and send the Excel file and "label file" to me (view [Wiki Documentation](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/wiki/Documentation) for how to use the labeling software).


# Contact

Errors or issues can be reported via [Issues](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/issues) and questions or comments can be created in the [Discussions](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/discussions). To get in direct contact, then you can connect via the built-in "Feedback" tab in the _CRT_ application.


# Documentation of data

Please refer to the [Wiki Documentation](https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/wiki/Documentation) page for adding or modifying the data.


# FAQ

* **Will it work on Linux with Wine or Mono?**
  - Best answer is "_maybe, but I hope_".
  - It has been tested on an old computer with "ZorinOS", having Wine and Mono installed. It did launch and worked, but it was very slow, which I tend to believe was due to the old hardware and no dedicated grahich card or native driver.
* **Will it work on Windows 7?**
  - Best answer is "_I expect it will_".
  - The requirements have been lowered, so it should support a Windows 7 having Service Pack 1 installed (and .NET Framework 4.8).
* **Why not creating this as a web page instead?**
  - Because I like the responsiveness and feel of a real native Windows application :-)
  - A web page will be for someone else to create, as that would be very heavy on the client-side, and this is not my favorite way of coding.
  - A web page will eventually disappear for various reasons, such as the site owner losing interest, technical problems, or simply because people eventually pass away and the domain cease to exists. _CRT_ will continue to work as long as it is available on your Windows computer.
* **Will you port this to Mac or Linux?**
  - Not any day soon. Maybe one day when AI becomes better at handling large complex tasks, then I could look in to this, but here-and-now I will only focus on the Windows application as-is.


# Data sources used for configurations

I have taken data from many, many places and I have given up crediting every small information I get from where on the internet. I do have a few _go-to_ places I frequently have visited:
- [My Old Computer](https://myoldcomputer.nl/technical-info/mainboards/) (view below also)
- [Zimmers](https://www.zimmers.net/anonftp/pub/cbm/schematics/computers/)
- [Ray Carlsen](https://portcommodore.com/rcarlsen/cbm/)

In many cases I provided or modified the data myself, which is why some expert facts-check would be very much appreciated.

If you want to contribute with a major new or updated dataset, then please let me know, as I happily will show credits here for joint efforts. There is a large Commodore community, and I wish for collaboration into making this a great and trustworthy reference for Commodore reparing (well, on the Windows platform at least).


# Inspiration for creating this project

I have been repairing Commodore 64/128 for some years, but I still consider myself as a _beginner_ in this world of hardware (I am more a software person). I often forget _where_ and _what_ to check, and I struggle to find again all the relevant ressources and schematics to check, not to mention how to find the components in the schematics. I did often refer to the "Mainboards" section of [My Old Computer](https://myoldcomputer.nl/technical-info/mainboards/), and I noticed that Jeroen did have a prototype of an application named _Repair Help_, and it did have the easy layout I was looking for (I did get a copy of it). However, it was never finalized from him, so I took upon myself to create something similar, and a couple of years later I did come up with a very similar looking application, also utilizing some data directly from Jeroen's page (with permission from him, of course).

For now I will continue to add new and refine data for myself, when doing my diagnostics and repairing of Commodore equipment, and then I can only hope that others will take the initiative and time it takes to add additional hardware or help refine the existing data.
