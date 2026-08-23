using System;
using System.Collections.Generic;

namespace Handlers.Geometry
{
    // ###########################################################################################
    // Small viewport and collection helpers extracted from TabSchematics so they can be unit
    // tested. Nothing here touches a control.
    // ###########################################################################################
    public static class ViewportMath
    {
        // ###########################################################################################
        // Converts one wheel event into a zoom factor.
        //
        // Linux and macOS deliver many small high-resolution wheel deltas where Windows delivers
        // one notch of magnitude 1.0; treating every event as a full step made zooming very coarse
        // and aggressive there. A delta magnitude of exactly 1.0 reduces this to exactly baseFactor.
        // ###########################################################################################
        public static double ComputeWheelZoomFactor(double deltaY, double baseFactor)
        {
            double magnitude = Math.Clamp(Math.Abs(deltaY), 0.1, 3.0);
            double factor = Math.Pow(baseFactor, magnitude);
            return deltaY > 0 ? factor : 1.0 / factor;
        }

        // ###########################################################################################
        // Case-insensitive set equality for two string collections, without allocating when the
        // sizes already differ. Used to decide whether a selection actually changed.
        // ###########################################################################################
        public static bool SetEqualsOrdinalIgnoreCase(
            IReadOnlyCollection<string> left,
            IReadOnlyCollection<string> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            if (left.Count == 0)
            {
                return true;
            }

            var leftSet = new HashSet<string>(left, StringComparer.OrdinalIgnoreCase);
            return leftSet.SetEquals(right);
        }
    }
}
