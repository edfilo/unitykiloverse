#import <Foundation/Foundation.h>
#import <CoreMotion/CoreMotion.h>

extern "C" {
    
    CMPedometer* pedometer = nil;
    
    // Callback function pointer type
    typedef void (*PedometerCallback)(int steps, double distance, double startTimestamp, double endTimestamp);
    PedometerCallback onUpdateCallback = nil;

    void _StartPedometer(PedometerCallback callback) {
        if (![CMPedometer isStepCountingAvailable]) {
            NSLog(@"[PedometerPlugin] Step counting not available.");
            return;
        }

        if (pedometer == nil) {
            pedometer = [[CMPedometer alloc] init];
        }
        
        onUpdateCallback = callback;

        [pedometer startPedometerUpdatesFromDate:[NSDate date] withHandler:^(CMPedometerData * _Nullable pedometerData, NSError * _Nullable error) {
            if (error) {
                NSLog(@"[PedometerPlugin] Error: %@", error);
                return;
            }
            
            if (onUpdateCallback != nil) {
                int steps = [pedometerData.numberOfSteps intValue];
                double distance = [pedometerData.distance doubleValue]; // in meters
                double start = [pedometerData.startDate timeIntervalSince1970];
                double end = [pedometerData.endDate timeIntervalSince1970];
                
                dispatch_async(dispatch_get_main_queue(), ^{
                     onUpdateCallback(steps, distance, start, end);
                });
            }
        }];
    }

    void _StopPedometer() {
        if (pedometer != nil) {
            [pedometer stopPedometerUpdates];
            pedometer = nil;
        }
    }

    void _QueryPedometerData(double startTimestamp, double endTimestamp, PedometerCallback callback) {
        if (![CMPedometer isStepCountingAvailable]) {
            NSLog(@"[PedometerPlugin] Step counting not available.");
            return;
        }

        if (pedometer == nil) {
            pedometer = [[CMPedometer alloc] init];
        }

        NSDate *startDate = [NSDate dateWithTimeIntervalSince1970:startTimestamp];
        NSDate *endDate = [NSDate dateWithTimeIntervalSince1970:endTimestamp];

        [pedometer queryPedometerDataFromDate:startDate toDate:endDate withHandler:^(CMPedometerData * _Nullable pedometerData, NSError * _Nullable error) {
            if (error) {
                NSLog(@"[PedometerPlugin] Query Error: %@", error);
                return;
            }
            
            if (callback != nil) {
                int steps = [pedometerData.numberOfSteps intValue];
                double distance = [pedometerData.distance doubleValue];
                // Pass back the requested range (or actual range) to identify the request
                double start = [startDate timeIntervalSince1970];
                double end = [endDate timeIntervalSince1970];
                
                dispatch_async(dispatch_get_main_queue(), ^{
                     callback(steps, distance, start, end);
                });
            }
        }];
    }
}
