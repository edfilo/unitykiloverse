#import <Foundation/Foundation.h>
#import <AuthenticationServices/AuthenticationServices.h>
#import <CommonCrypto/CommonDigest.h>

@interface AppleSignInDelegate : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property (nonatomic, copy) NSString *callbackObject;
@property (nonatomic, copy) NSString *currentNonce;
@end

static ASAuthorizationController *appleAuthorizationController API_AVAILABLE(ios(13.0)) = nil;

@implementation AppleSignInDelegate

- (void)authorizationController:(ASAuthorizationController *)controller
   didCompleteWithAuthorization:(ASAuthorization *)authorization API_AVAILABLE(ios(13.0)) {

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

        // Build result string: "idToken|nonce|fullName|appleUserId"
        NSString *result = [NSString stringWithFormat:@"%@|%@|%@|%@",
                          idToken ?: @"",
                          self.currentNonce ?: @"",
                          fullName,
                          appleUserId];

        // Send to Unity
        UnitySendMessage([self.callbackObject UTF8String],
                        "OnAppleSignInCallback",
                        [result UTF8String]);
        appleAuthorizationController = nil;
    }
}

- (void)authorizationController:(ASAuthorizationController *)controller
           didCompleteWithError:(NSError *)error API_AVAILABLE(ios(13.0)) {

    NSString *errorMessage = error.localizedDescription;

    // Don't treat cancellation as an error
    if (error.code == ASAuthorizationErrorCanceled) {
        errorMessage = @"User canceled";
    }

    UnitySendMessage([self.callbackObject UTF8String],
                    "OnAppleSignInError",
                    [errorMessage UTF8String]);
    appleAuthorizationController = nil;
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller API_AVAILABLE(ios(13.0)) {
    return UnityGetGLView().window;
}

@end

// Singleton delegate instance
static AppleSignInDelegate *appleSignInDelegate = nil;

// Generate random nonce
NSString* generateNonce(NSInteger length) {
    NSString *charset = @"0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-._";
    NSMutableString *result = [NSMutableString stringWithCapacity:length];

    for (NSInteger i = 0; i < length; i++) {
        uint32_t randomIndex = arc4random_uniform((uint32_t)[charset length]);
        unichar character = [charset characterAtIndex:randomIndex];
        [result appendFormat:@"%C", character];
    }

    return result;
}

// SHA256 hash
NSString* sha256(NSString *input) {
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
    // Check if Sign in with Apple is available
    bool _IsAppleSignInAvailable() {
        if (@available(iOS 13.0, *)) {
            return YES;
        }
        return NO;
    }

    // Trigger Sign in with Apple
    void _SignInWithApple(const char* callbackObject) {
        if (@available(iOS 13.0, *)) {
            // Create delegate if needed
            if (appleSignInDelegate == nil) {
                appleSignInDelegate = [[AppleSignInDelegate alloc] init];
            }

            // Generate nonce
            NSString *nonce = generateNonce(32);
            appleSignInDelegate.currentNonce = nonce;
            appleSignInDelegate.callbackObject = [NSString stringWithUTF8String:callbackObject];

            // Create request
            ASAuthorizationAppleIDProvider *appleIDProvider = [[ASAuthorizationAppleIDProvider alloc] init];
            ASAuthorizationAppleIDRequest *request = [appleIDProvider createRequest];
            request.requestedScopes = @[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail];
            request.nonce = sha256(nonce);

            // Create and start authorization controller
            appleAuthorizationController =
                [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];

            appleAuthorizationController.delegate = appleSignInDelegate;
            appleAuthorizationController.presentationContextProvider = appleSignInDelegate;

            [appleAuthorizationController performRequests];
        }
    }
}
