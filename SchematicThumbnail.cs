using Avalonia.Media;

namespace CRT
{
    // ###########################################################################################
    // Represents a single schematic thumbnail item for display in the Schematics tab gallery.
    // ###########################################################################################
    public class SchematicThumbnail
    {
        public string Name { get; init; } = string.Empty;
        public string ImageFilePath { get; init; } = string.Empty;
        public IImage? ImageSource { get; init; }
    }
}