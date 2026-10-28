#import <UIKit/UIKit.h>
#import <CoreHaptics/CoreHaptics.h>

extern "C" bool FrontierHapticsSupported()
{
    if (@available(iOS 13.0, *))
        return [[CHHapticEngine capabilitiesForHardware] supportsHaptics];
    return false;
}

extern "C" void FrontierHapticsPulse(float strength)
{
    if (!FrontierHapticsSupported()) return;
    if (@available(iOS 13.0, *))
    {
        static UIImpactFeedbackGenerator *generator = nil;
        if (generator == nil)
            generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        [generator impactOccurredWithIntensity:strength];
        [generator prepare];
    }
}
