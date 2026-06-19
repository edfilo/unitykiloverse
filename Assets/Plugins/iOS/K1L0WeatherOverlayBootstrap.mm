#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern "C" void K1L0InstallWeatherOverlay(void);

__attribute__((constructor))
static void K1L0WeatherOverlayBootstrap(void)
{
    [[NSNotificationCenter defaultCenter] addObserverForName:UIApplicationDidBecomeActiveNotification
                                                      object:nil
                                                       queue:[NSOperationQueue mainQueue]
                                                  usingBlock:^(__unused NSNotification *note) {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.8 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            K1L0InstallWeatherOverlay();
        });
    }];
}
