#import <Foundation/Foundation.h>

extern "C" {
    // Returns iOS thermal state: 0=nominal, 1=fair, 2=serious, 3=critical
    int _GetThermalState() {
        if (@available(iOS 11.0, *)) {
            return (int)[[NSProcessInfo processInfo] thermalState];
        }
        return 0;
    }
}
