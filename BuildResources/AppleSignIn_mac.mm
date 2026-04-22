// Source for Assets/Plugins/macOS/AppleSignIn.bundle.
// Kept outside Plugins/ so Unity doesn't try to compile it.
// Rebuild with BuildResources/build_applesignin_bundle.sh.
//
// Mac Unity standalone does NOT export UnitySendMessage, so we use
// function-pointer callbacks passed in from C# via MonoPInvokeCallback.

#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#import <AuthenticationServices/AuthenticationServices.h>
#import <CommonCrypto/CommonDigest.h>

typedef void (*AppleSignInSuccessCallback)(const char* result);
typedef void (*AppleSignInErrorCallback)(const char* error);

@interface MacAppleSignInDelegate : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property (nonatomic, copy) NSString *currentNonce;
@property (nonatomic, assign) AppleSignInSuccessCallback successCallback;
@property (nonatomic, assign) AppleSignInErrorCallback errorCallback;
@end

@implementation MacAppleSignInDelegate

- (void)authorizationController:(ASAuthorizationController *)controller
   didCompleteWithAuthorization:(ASAuthorization *)authorization API_AVAILABLE(macos(10.15)) {

    if ([authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]]) {
        ASAuthorizationAppleIDCredential *appleIDCredential = authorization.credential;

        NSString *idToken = [[NSString alloc] initWithData:appleIDCredential.identityToken
                                                  encoding:NSUTF8StringEncoding];

        NSString *fullName = @"";
        if (appleIDCredential.fullName) {
            NSPersonNameComponents *nameComponents = appleIDCredential.fullName;
            NSMutableArray *nameParts = [NSMutableArray array];
            if (nameComponents.givenName) [nameParts addObject:nameComponents.givenName];
            if (nameComponents.familyName) [nameParts addObject:nameComponents.familyName];
            fullName = [nameParts componentsJoinedByString:@" "];
        }

        NSString *appleUserId = appleIDCredential.user ?: @"";

        NSString *result = [NSString stringWithFormat:@"%@|%@|%@|%@",
                          idToken ?: @"",
                          self.currentNonce ?: @"",
                          fullName,
                          appleUserId];

        if (self.successCallback) {
            self.successCallback([result UTF8String]);
        }
    }
}

- (void)authorizationController:(ASAuthorizationController *)controller
           didCompleteWithError:(NSError *)error API_AVAILABLE(macos(10.15)) {

    NSString *errorMessage = error.localizedDescription;
    if (error.code == ASAuthorizationErrorCanceled) {
        errorMessage = @"User canceled";
    }

    if (self.errorCallback) {
        self.errorCallback([errorMessage UTF8String]);
    }
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller API_AVAILABLE(macos(10.15)) {
    NSWindow *window = [[NSApplication sharedApplication] keyWindow];
    if (!window) window = [[NSApplication sharedApplication] mainWindow];
    if (!window) {
        NSArray<NSWindow *> *windows = [[NSApplication sharedApplication] windows];
        if (windows.count > 0) window = windows.firstObject;
    }
    return window;
}

@end

static MacAppleSignInDelegate *macAppleSignInDelegate = nil;

static NSString* macGenerateNonce(NSInteger length) {
    NSString *charset = @"0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-._";
    NSMutableString *result = [NSMutableString stringWithCapacity:length];
    for (NSInteger i = 0; i < length; i++) {
        uint32_t randomIndex = arc4random_uniform((uint32_t)[charset length]);
        unichar character = [charset characterAtIndex:randomIndex];
        [result appendFormat:@"%C", character];
    }
    return result;
}

static NSString* macSha256(NSString *input) {
    const char *cstr = [input cStringUsingEncoding:NSUTF8StringEncoding];
    NSData *data = [NSData dataWithBytes:cstr length:input.length];

    uint8_t digest[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256(data.bytes, (CC_LONG)data.length, digest);

    NSMutableString *output = [NSMutableString stringWithCapacity:CC_SHA256_DIGEST_LENGTH * 2];
    for (int i = 0; i < CC_SHA256_DIGEST_LENGTH; i++) {
        [output appendFormat:@"%02x", digest[i]];
    }
    return output;
}

extern "C" {
    bool _IsAppleSignInAvailable() {
        if (@available(macOS 10.15, *)) {
            return YES;
        }
        return NO;
    }

    void _SignInWithApple(AppleSignInSuccessCallback onSuccess, AppleSignInErrorCallback onError) {
        if (@available(macOS 10.15, *)) {
            if (macAppleSignInDelegate == nil) {
                macAppleSignInDelegate = [[MacAppleSignInDelegate alloc] init];
            }

            NSString *nonce = macGenerateNonce(32);
            macAppleSignInDelegate.currentNonce = nonce;
            macAppleSignInDelegate.successCallback = onSuccess;
            macAppleSignInDelegate.errorCallback = onError;

            ASAuthorizationAppleIDProvider *appleIDProvider = [[ASAuthorizationAppleIDProvider alloc] init];
            ASAuthorizationAppleIDRequest *request = [appleIDProvider createRequest];
            request.requestedScopes = @[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail];
            request.nonce = macSha256(nonce);

            ASAuthorizationController *authorizationController =
                [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];

            authorizationController.delegate = macAppleSignInDelegate;
            authorizationController.presentationContextProvider = macAppleSignInDelegate;

            [authorizationController performRequests];
        }
    }
}
