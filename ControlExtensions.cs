using System.Reflection;

/*
------- 
CHATGPT
-------

What is Double Buffering?
Double buffering is a technique used in graphics programming to reduce flickering 
and improve the rendering performance of a graphical user interface (GUI). 
In GUI applications, flickering can occur when elements are frequently redrawn 
on the screen, leading to a less pleasant user experience.

How Double Buffering Works:

1) Single Buffering (Default): In a typical GUI application without double buffering, 
all drawing operations occur directly on the screen. When you draw something, it 
immediately appears on the screen. This can result in flickering, especially when 
updating elements rapidly.

2) Double Buffering: With double buffering, you have two off-screen buffers: one for 
drawing (the back buffer) and one for displaying (the front buffer). Drawing operations 
are performed on the back buffer, which is not visible to the user. When everything is 
ready for display, the back buffer is swapped with the front buffer. This swap happens 
so quickly that it appears as if the drawing occurred instantaneously, reducing flickering.

Why Enable Double Buffering?
Enabling double buffering can be beneficial in scenarios where you have dynamic or 
frequently updated graphical elements, such as animations, resizing, or custom-drawn 
controls. When you enable double buffering, the drawing is first done off-screen, and 
the entire updated frame is presented to the user all at once, which creates a smoother 
and flicker-free experience.

Using the DoubleBuffered Extension Method:
In the code I provided earlier, we created an extension method called DoubleBuffered for 
the Control class. This method allows you to easily enable double buffering for any 
control, such as panels, within your application.

Here's a brief overview of how the extension method works:

* It uses reflection to access the non-public DoubleBuffered property of a Control.
* It sets the DoubleBuffered property to true or false, depending on the value you 
* pass to the method (true to enable double buffering, false to disable it).
* 
By enabling double buffering on controls that frequently update or need to be redrawn, you can achieve smoother rendering and reduce flickering, as you've observed in your application.

Overall, double buffering is a valuable technique for enhancing the user experience in graphics-intensive applications and scenarios where dynamic updates are common.
 
*/

public static class ControlExtensions
{
    public static void DoubleBuffered(this Control control, bool enable)
    {
        var doubleBufferPropertyInfo = control.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        doubleBufferPropertyInfo?.SetValue(control, enable, null);
    }
}