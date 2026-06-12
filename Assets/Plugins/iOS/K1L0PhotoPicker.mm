#import <UIKit/UIKit.h>
#import <AVFoundation/AVFoundation.h>

extern "C" void UnitySendMessage(const char *, const char *, const char *);

@interface K1L0PhotoPickerDelegate : NSObject <UIImagePickerControllerDelegate, UINavigationControllerDelegate>
@property(nonatomic, copy) NSString *gameObject;
@property(nonatomic, copy) NSString *callback;
@property(nonatomic, strong) UIImagePickerController *picker;
@end

@interface K1L0CameraViewController : UIViewController <AVCaptureVideoDataOutputSampleBufferDelegate>
@property(nonatomic, copy) NSString *gameObject;
@property(nonatomic, copy) NSString *callback;
@property(nonatomic, strong) AVCaptureSession *session;
@property(nonatomic, strong) AVCaptureVideoDataOutput *videoOutput;
@property(nonatomic, strong) AVCaptureVideoPreviewLayer *previewLayer;
@property(nonatomic, strong) UIButton *captureButton;
@property(nonatomic, strong) UIImage *latestFrame;
@property(nonatomic, strong) dispatch_queue_t frameQueue;
@property(nonatomic, assign) NSTimeInterval lastFrameTime;
@end

static K1L0PhotoPickerDelegate *k1l0PhotoPickerDelegate;
static K1L0CameraViewController *k1l0CameraViewController;

static NSString *K1L0WriteJPEG(NSData *jpeg) {
    if (!jpeg || jpeg.length == 0) return @"";
    NSString *documents = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES).firstObject;
    NSString *path = [documents stringByAppendingPathComponent:@"k1l0_tx_latest.jpg"];
    [jpeg writeToFile:path atomically:YES];
    return path;
}

static UIImage *K1L0NormalizedVerticalImage(UIImage *image) {
    if (!image) return nil;
    CGSize size = image.size;
    if (size.width > size.height) {
        UIGraphicsBeginImageContextWithOptions(CGSizeMake(size.height, size.width), YES, image.scale);
        CGContextRef ctx = UIGraphicsGetCurrentContext();
        CGContextTranslateCTM(ctx, size.height * 0.5, size.width * 0.5);
        CGContextRotateCTM(ctx, M_PI_2);
        [image drawInRect:CGRectMake(-size.width * 0.5, -size.height * 0.5, size.width, size.height)];
        UIImage *rotated = UIGraphicsGetImageFromCurrentImageContext();
        UIGraphicsEndImageContext();
        return rotated;
    }
    if (image.imageOrientation == UIImageOrientationUp) return image;
    UIGraphicsBeginImageContextWithOptions(size, YES, image.scale);
    [image drawInRect:CGRectMake(0, 0, size.width, size.height)];
    UIImage *normalized = UIGraphicsGetImageFromCurrentImageContext();
    UIGraphicsEndImageContext();
    return normalized;
}

@implementation K1L0PhotoPickerDelegate
- (void)imagePickerController:(UIImagePickerController *)picker didFinishPickingMediaWithInfo:(NSDictionary<UIImagePickerControllerInfoKey,id> *)info {
    UIImage *image = info[UIImagePickerControllerEditedImage] ?: info[UIImagePickerControllerOriginalImage];
    image = K1L0NormalizedVerticalImage(image);
    NSString *path = image ? K1L0WriteJPEG(UIImageJPEGRepresentation(image, 0.88)) : @"";
    [picker dismissViewControllerAnimated:YES completion:^{
        UnitySendMessage(self.gameObject.UTF8String, self.callback.UTF8String, path.UTF8String);
        self.picker = nil;
    }];
}

- (void)imagePickerControllerDidCancel:(UIImagePickerController *)picker {
    [picker dismissViewControllerAnimated:YES completion:^{
        UnitySendMessage(self.gameObject.UTF8String, self.callback.UTF8String, "");
        self.picker = nil;
    }];
}
@end

@implementation K1L0CameraViewController
- (void)viewDidLoad {
    [super viewDidLoad];
    self.view.backgroundColor = UIColor.blackColor;

    self.session = [AVCaptureSession new];
    self.session.sessionPreset = AVCaptureSessionPresetHigh;
    self.videoOutput = [AVCaptureVideoDataOutput new];
    self.videoOutput.alwaysDiscardsLateVideoFrames = YES;
    self.videoOutput.videoSettings = @{(NSString *)kCVPixelBufferPixelFormatTypeKey: @(kCVPixelFormatType_32BGRA)};
    self.frameQueue = dispatch_queue_create("com.filowatt.k1lo.camera.frames", DISPATCH_QUEUE_SERIAL);
    [self.videoOutput setSampleBufferDelegate:self queue:self.frameQueue];

    AVCaptureDevice *device = [AVCaptureDevice defaultDeviceWithMediaType:AVMediaTypeVideo];
    NSError *error = nil;
    AVCaptureDeviceInput *input = device ? [AVCaptureDeviceInput deviceInputWithDevice:device error:&error] : nil;

    [self.session beginConfiguration];
    if (input && [self.session canAddInput:input]) [self.session addInput:input];
    if ([self.session canAddOutput:self.videoOutput]) [self.session addOutput:self.videoOutput];
    AVCaptureConnection *videoConnection = [self.videoOutput connectionWithMediaType:AVMediaTypeVideo];
    if (videoConnection.isVideoOrientationSupported) videoConnection.videoOrientation = AVCaptureVideoOrientationPortrait;
    [self.session commitConfiguration];

    self.previewLayer = [AVCaptureVideoPreviewLayer layerWithSession:self.session];
    self.previewLayer.videoGravity = AVLayerVideoGravityResizeAspectFill;
    self.previewLayer.frame = self.view.bounds;
    AVCaptureConnection *previewConnection = self.previewLayer.connection;
    if (previewConnection.isVideoOrientationSupported) previewConnection.videoOrientation = AVCaptureVideoOrientationPortrait;
    [self.view.layer addSublayer:self.previewLayer];

    UIView *bar = [[UIView alloc] initWithFrame:CGRectMake(0, self.view.bounds.size.height - 124, self.view.bounds.size.width, 124)];
    bar.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleTopMargin;
    bar.backgroundColor = [UIColor colorWithWhite:0 alpha:0.72];
    [self.view addSubview:bar];

    self.captureButton = [UIButton buttonWithType:UIButtonTypeSystem];
    self.captureButton.frame = CGRectMake((bar.bounds.size.width - 220) * 0.5, 26, 220, 62);
    self.captureButton.autoresizingMask = UIViewAutoresizingFlexibleLeftMargin | UIViewAutoresizingFlexibleRightMargin;
    self.captureButton.layer.borderWidth = 1.5;
    self.captureButton.layer.borderColor = UIColor.greenColor.CGColor;
    self.captureButton.layer.cornerRadius = 6;
    self.captureButton.enabled = NO;
    [self.captureButton setTitle:@"STARTING…" forState:UIControlStateDisabled];
    [self.captureButton setTitle:@"TAKE PHOTO" forState:UIControlStateNormal];
    [self.captureButton setTitleColor:UIColor.greenColor forState:UIControlStateNormal];
    self.captureButton.titleLabel.font = [UIFont monospacedSystemFontOfSize:18 weight:UIFontWeightSemibold];
    [self.captureButton addTarget:self action:@selector(captureTapped:) forControlEvents:UIControlEventTouchUpInside];
    [bar addSubview:self.captureButton];

    UIButton *cancel = [UIButton buttonWithType:UIButtonTypeSystem];
    cancel.frame = CGRectMake(self.view.bounds.size.width - 76, 46, 56, 48);
    cancel.autoresizingMask = UIViewAutoresizingFlexibleLeftMargin | UIViewAutoresizingFlexibleBottomMargin;
    [cancel setTitle:@"×" forState:UIControlStateNormal];
    [cancel setTitleColor:UIColor.whiteColor forState:UIControlStateNormal];
    cancel.titleLabel.font = [UIFont systemFontOfSize:38 weight:UIFontWeightRegular];
    [cancel addTarget:self action:@selector(cancelTapped:) forControlEvents:UIControlEventTouchUpInside];
    [self.view addSubview:cancel];
}

- (void)viewDidLayoutSubviews {
    [super viewDidLayoutSubviews];
    self.previewLayer.frame = self.view.bounds;
}

- (void)viewDidAppear:(BOOL)animated {
    [super viewDidAppear:animated];
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        [self.session startRunning];
        dispatch_async(dispatch_get_main_queue(), ^{
            AVCaptureConnection *connection = [self.videoOutput connectionWithMediaType:AVMediaTypeVideo];
            BOOL ready = self.session.isRunning && connection && connection.isEnabled;
            self.captureButton.enabled = ready;
            [self.captureButton setTitle:(ready ? @"TAKE PHOTO" : @"CAMERA NOT READY") forState:UIControlStateDisabled];
        });
    });
}

- (void)viewWillDisappear:(BOOL)animated {
    [super viewWillDisappear:animated];
    [self.session stopRunning];
}

- (void)captureTapped:(UIButton *)sender {
    UIImage *image = self.latestFrame;
    if (!self.session.isRunning || !image) {
        sender.enabled = NO;
        [sender setTitle:@"STARTING…" forState:UIControlStateDisabled];
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.75 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            BOOL ready = self.session.isRunning && self.latestFrame != nil;
            sender.enabled = ready;
            [sender setTitle:(ready ? @"TAKE PHOTO" : @"STARTING…") forState:UIControlStateDisabled];
        });
        return;
    }

    sender.enabled = NO;
    [sender setTitle:@"CAPTURING…" forState:UIControlStateDisabled];
    image = K1L0NormalizedVerticalImage(image);
    NSString *path = image ? K1L0WriteJPEG(UIImageJPEGRepresentation(image, 0.88)) : @"";
    [self dismissViewControllerAnimated:YES completion:^{
        UnitySendMessage(self.gameObject.UTF8String, self.callback.UTF8String, path.UTF8String);
        k1l0CameraViewController = nil;
    }];
}

- (void)cancelTapped:(UIButton *)sender {
    [self dismissViewControllerAnimated:YES completion:^{
        UnitySendMessage(self.gameObject.UTF8String, self.callback.UTF8String, "");
        k1l0CameraViewController = nil;
    }];
}

- (void)captureOutput:(AVCaptureOutput *)output didOutputSampleBuffer:(CMSampleBufferRef)sampleBuffer fromConnection:(AVCaptureConnection *)connection {
    NSTimeInterval now = CACurrentMediaTime();
    if (now - self.lastFrameTime < 0.20) return;
    self.lastFrameTime = now;

    CVImageBufferRef imageBuffer = CMSampleBufferGetImageBuffer(sampleBuffer);
    if (!imageBuffer) return;

    static CIContext *context = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        context = [CIContext contextWithOptions:nil];
    });

    CIImage *ciImage = [CIImage imageWithCVPixelBuffer:imageBuffer];
    CGRect extent = ciImage.extent;
    CGImageRef cgImage = [context createCGImage:ciImage fromRect:extent];
    if (!cgImage) return;

    UIImage *image = [UIImage imageWithCGImage:cgImage scale:1.0 orientation:UIImageOrientationRight];
    CGImageRelease(cgImage);

    dispatch_async(dispatch_get_main_queue(), ^{
        self.latestFrame = K1L0NormalizedVerticalImage(image);
        if (!self.captureButton.enabled && self.session.isRunning) {
            self.captureButton.enabled = YES;
            [self.captureButton setTitle:@"TAKE PHOTO" forState:UIControlStateNormal];
        }
    });
}
@end

static UIViewController *K1L0TopViewController(void) {
    UIViewController *root = UIApplication.sharedApplication.keyWindow.rootViewController;
    if (!root) {
        for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
            if (scene.activationState != UISceneActivationStateForegroundActive || ![scene isKindOfClass:UIWindowScene.class]) continue;
            for (UIWindow *window in ((UIWindowScene *)scene).windows) {
                if (window.isKeyWindow) {
                    root = window.rootViewController;
                    break;
                }
            }
            if (root) break;
        }
    }
    while (root.presentedViewController) root = root.presentedViewController;
    return root;
}

extern "C" void K1L0PickPhoto(const char *gameObject, const char *callback) {
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *root = K1L0TopViewController();
        if (!root) {
            UnitySendMessage(gameObject, callback, "");
            return;
        }

        if ([UIImagePickerController isSourceTypeAvailable:UIImagePickerControllerSourceTypeCamera]) {
            k1l0CameraViewController = [K1L0CameraViewController new];
            k1l0CameraViewController.gameObject = [NSString stringWithUTF8String:gameObject];
            k1l0CameraViewController.callback = [NSString stringWithUTF8String:callback];
            k1l0CameraViewController.modalPresentationStyle = UIModalPresentationFullScreen;
            [root presentViewController:k1l0CameraViewController animated:YES completion:nil];
            return;
        }

        if (![UIImagePickerController isSourceTypeAvailable:UIImagePickerControllerSourceTypePhotoLibrary]) {
            UnitySendMessage(gameObject, callback, "");
            return;
        }

        k1l0PhotoPickerDelegate = [K1L0PhotoPickerDelegate new];
        k1l0PhotoPickerDelegate.gameObject = [NSString stringWithUTF8String:gameObject];
        k1l0PhotoPickerDelegate.callback = [NSString stringWithUTF8String:callback];

        UIImagePickerController *picker = [UIImagePickerController new];
        picker.sourceType = UIImagePickerControllerSourceTypePhotoLibrary;
        picker.modalPresentationStyle = UIModalPresentationFullScreen;
        picker.allowsEditing = NO;
        picker.delegate = k1l0PhotoPickerDelegate;
        k1l0PhotoPickerDelegate.picker = picker;
        [root presentViewController:picker animated:YES completion:nil];
    });
}
