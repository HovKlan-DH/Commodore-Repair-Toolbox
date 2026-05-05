using System.Collections.Generic;

namespace Handlers.DataHandling
{
    public class BoardSchematicEntry
    {
        public string UuidV4 { get; init; } = string.Empty;
        public string SchematicName { get; init; } = string.Empty;
        public string CadName { get; init; } = string.Empty;
        public string SchematicImageFile { get; init; } = string.Empty;
        public string SchematicHighlightColor { get; init; } = string.Empty;
        public string SchematicHighlightOpacity { get; init; } = string.Empty;
        public string OppositeTraceHighlightColor { get; init; } = string.Empty;
        public string ThumbnailHighlightColor { get; init; } = string.Empty;
        public string ThumbnailHighlightOpacity { get; init; } = string.Empty;
    }

    public class ComponentEntry
    {
        public string UuidV4 { get; init; } = string.Empty;
        public string BoardLabel { get; init; } = string.Empty;
        public string FriendlyName { get; init; } = string.Empty;
        public string TechnicalNameOrValue { get; init; } = string.Empty;
        public string PartNumber { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    public class ComponentImageEntry
    {
        public string UuidV4 { get; init; } = string.Empty;
        public string BoardLabel { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
        public string Pin { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string ExpectedOscilloscopeReading { get; init; } = string.Empty;
        public string File { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
        public string TimeDiv { get; init; } = string.Empty;
        public string VoltsDiv { get; init; } = string.Empty;
        public string TriggerLevelVolts { get; init; } = string.Empty;
    }

    public class ComponentHighlightEntry
    {
        public string SchematicName { get; init; } = string.Empty;
        public string BoardLabel { get; init; } = string.Empty;
        public string X { get; init; } = string.Empty;
        public string Y { get; init; } = string.Empty;
        public string Width { get; init; } = string.Empty;
        public string Height { get; init; } = string.Empty;
    }

    public class ComponentLocalFileEntry
    {
        public string UuidV4 { get; init; } = string.Empty;
        public string BoardLabel { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string File { get; init; } = string.Empty;
    }

    public class ComponentLinkEntry
    {
        public string UuidV4 { get; init; } = string.Empty;
        public string BoardLabel { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
    }

    public class BoardLocalFileEntry
    {
        public string UuidV4 { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string File { get; init; } = string.Empty;
    }

    public class BoardLinkEntry
    {
        public string UuidV4 { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
    }

    public class CreditEntry
    {
        public string UuidV4 { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string SubCategory { get; init; } = string.Empty;
        public string NameOrHandle { get; init; } = string.Empty;
        public string Contact { get; init; } = string.Empty;
    }

    public class KiCadImportantSignalEntry
    {
        public string DisplayName { get; init; } = string.Empty;
        public string KiCadNetName { get; init; } = string.Empty;
    }

    // ###########################################################################################
    // Container for all data loaded from a board-specific Excel file.
    // IsLoaded is true only when the file was read successfully.
    // ###########################################################################################
    public class BoardData
    {
        public string RevisionDate { get; init; } = string.Empty;
        public List<BoardSchematicEntry> Schematics { get; init; } = new();
        public List<ComponentEntry> Components { get; init; } = new();
        public List<ComponentImageEntry> ComponentImages { get; init; } = new();
        public List<ComponentHighlightEntry> ComponentHighlights { get; init; } = new();
        public List<ComponentLocalFileEntry> ComponentLocalFiles { get; init; } = new();
        public List<ComponentLinkEntry> ComponentLinks { get; init; } = new();
        public List<BoardLocalFileEntry> BoardLocalFiles { get; init; } = new();
        public List<BoardLinkEntry> BoardLinks { get; init; } = new();
        public List<CreditEntry> Credits { get; init; } = new();
        public List<KiCadImportantSignalEntry> KiCadImportantSignals { get; init; } = new();
    }

    public class OscilloscopeEntry
    {
        public string Brand { get; init; } = string.Empty;
        public string SeriesOrModel { get; init; } = string.Empty;
        public string Port { get; init; } = string.Empty;

        public string Identify { get; init; } = string.Empty;
        public string DrainErrorQueue { get; init; } = string.Empty;
        public string OperationComplete { get; init; } = string.Empty;
        public string ClearStatistics { get; init; } = string.Empty;
        public string QueryActiveTrigger { get; init; } = string.Empty;

        public string Stop { get; init; } = string.Empty;
        public string Single { get; init; } = string.Empty;
        public string Run { get; init; } = string.Empty;

        public string QueryTriggerMode { get; init; } = string.Empty;
        public string QueryTriggerSource { get; init; } = string.Empty;
        public string SetTriggerSource { get; init; } = string.Empty;
        public string QueryTriggerSlope { get; init; } = string.Empty;
        public string SetTriggerSlope { get; init; } = string.Empty;
        public string QueryTriggerLevel { get; init; } = string.Empty;
        public string SetTriggerLevel { get; init; } = string.Empty;

        public string QueryAvgCount { get; init; } = string.Empty;
        public string SetAvgCount { get; init; } = string.Empty;
        public string QueryMemoryDepth { get; init; } = string.Empty;
        public string SetMemoryDepth { get; init; } = string.Empty;
        public string QuerySampleRate { get; init; } = string.Empty;

        public string QueryProbeAttenuation { get; init; } = string.Empty;
        public string SetProbeAttenuation { get; init; } = string.Empty;
        public string QueryChannelScale { get; init; } = string.Empty;
        public string SetChannelScale { get; init; } = string.Empty;
        public string QueryChannelOffset { get; init; } = string.Empty;
        public string SetChannelOffset { get; init; } = string.Empty;

        public string QueryTimeScale { get; init; } = string.Empty;
        public string SetTimeScale { get; init; } = string.Empty;
        public string QueryTimeOffset { get; init; } = string.Empty;
        public string SetTimeOffset { get; init; } = string.Empty;

        public string QueryTimeDiv { get; init; } = string.Empty;
        public string SetTimeDiv { get; init; } = string.Empty;
        public string QueryVoltsDiv { get; init; } = string.Empty;
        public string SetVoltsDiv { get; init; } = string.Empty;

        public string QueryMeasureFrequency { get; init; } = string.Empty;
        public string QueryMeasurePeriod { get; init; } = string.Empty;
        public string QueryMeasureDutyCycle { get; init; } = string.Empty;
        public string QueryMeasureRiseTime { get; init; } = string.Empty;
        public string QueryMeasureFallTime { get; init; } = string.Empty;
        public string QueryMeasureOvershoot { get; init; } = string.Empty;
        public string QueryMeasurePreshoot { get; init; } = string.Empty;
        public string QueryMeasureAmplitude { get; init; } = string.Empty;
        public string QueryMeasurePkToPk { get; init; } = string.Empty;
        public string QueryMeasureMaximum { get; init; } = string.Empty;
        public string QueryMeasureMinimum { get; init; } = string.Empty;
        public string QueryMeasureMean { get; init; } = string.Empty;
        public string QueryMeasureRms { get; init; } = string.Empty;

        public string QueryCursorHorizontal { get; init; } = string.Empty;
        public string SetCursorHorizontal { get; init; } = string.Empty;
        public string QueryCursorVertical { get; init; } = string.Empty;
        public string SetCursorVertical { get; init; } = string.Empty;

        public string ReadWaveformPreamble { get; init; } = string.Empty;
        public string ReadWaveformData { get; init; } = string.Empty;
        public string DumpImage { get; init; } = string.Empty;
        public string ScreenshotCommand { get; init; } = string.Empty;

        public string TimeDivList { get; init; } = string.Empty;
        public string VoltsDivList { get; init; } = string.Empty;
        public string DebounceTime { get; init; } = string.Empty;

        public string DefaultFileExtension { get; init; } = string.Empty;
        public string Notes { get; init; } = string.Empty;
    }
}