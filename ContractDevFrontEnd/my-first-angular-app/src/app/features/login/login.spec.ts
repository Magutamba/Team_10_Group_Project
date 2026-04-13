import { ComponentFixture, TestBed } from '@angular/core/testing';
import { throwError, of } from 'rxjs';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { Login } from './login';
import { AuthService } from '../../core/auth/auth.service';
import { UserService } from '../../core/services/user.service';
import { Profile } from '../../core/models/profile.models';
import { HttpResponse } from '@angular/common/http';
import { fakeAsync, tick } from '@angular/core/testing';

describe('Login', () => {
  let authServiceSpyObj: jasmine.SpyObj<AuthService>;
  let userServiceSpyObj: jasmine.SpyObj<UserService>;
  let router: Router;

  //Testing instance of the Login component
  let component: Login;
  //Wrapper for Login HTML elements
  let fixture: ComponentFixture<Login>;

  beforeEach(async () => {
    //Jasime service that mimics the AuthService - allows for mock HTTP calls, no dependance on backend status
    authServiceSpyObj = jasmine.createSpyObj('AuthService', ['login', 'getCurrentUserId']);
    //Another spy object to mimic UserService for successful login attempts, needs to be created before Login test bed is built
    userServiceSpyObj = jasmine.createSpyObj('UserService', ['getProfile']);
    //Inject mock AuthService to Login for testing mock HTTP calls
    //ProvideRouter allows for injectable router later in setup - detection of redirects
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        { provide: AuthService, useValue: authServiceSpyObj },
        { provide: UserService, useValue: userServiceSpyObj },
        provideRouter([]),
      ],
    }).compileComponents();

    //Inject router component to verify if login attempts causes redirect - used to test valid and invalid login attempts
    //Overrides real router dependency in login.ts to prevent errors caused by real navigation
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('shows login success when credentials are correct', fakeAsync(() => {
    //Arrange
    //Simulate HTTP call and force error for when login is eventually handled
    authServiceSpyObj.login.and.returnValue(
      of(new HttpResponse({ status: 200, body: { token: 'TOKEN-123.456.7890' } })),
    );
    //Simulate retrieving user id from token
    authServiceSpyObj.getCurrentUserId.and.returnValue(1);

    //Create mock profile to inject into UserService to simulate successful profile retrieval
    const mockProfile: Profile = {
      userId: 1,
      userTitle: 'title',
      firstName: 'Mock',
      lastName: 'Profile',
      username: 'Mock Profile',
      phoneNumber: '123',
      email: 'valid@email.com',
      country: 'US',
      description: 'developer',
      bio: '',
      availableForWork: false,
      offeringWork: false,
      displayUserName: false,
      hidePhoneNumber: false,
      profileImagePath: null,
      skills: [],
      selectedSkills: [],
      securityQuestion: '',
      socials: { ['']: null },
      ratings: { ['']: 0 },
      existingRating: null,
      facebookLink: '',
      userSocialEmailLink: '',
      xLink: '',
      gitHubLink: '',
      linkedinLink: '',
    };
    userServiceSpyObj.getProfile.and.returnValue(of(mockProfile));

    //Set value of email and password to fulfil login requirements - simulate values entered into form fields
    component.loginForm.setValue({ email: 'valid@email.com', password: 'password123' });

    //Act
    //Fire login request - will route through success chain as response has been hard coded to Status 200 Ok
    component.handleLogin();

    //Assert
    //Login should be called on mock AuthService
    expect(authServiceSpyObj.login).toHaveBeenCalled();
    //Error should remain empty during successful request
    expect(component.loginError()).toBe('');
    //Test must wait before checking router on successful request due to implementation in login.ts
    tick(3000);
    //After waiting router navigation should have been called
    expect(router.navigate).toHaveBeenCalled();
  }));

  it('shows login error when credentials are incorrect', () => {
    //Arrange
    //Simulate HTTP call and force error for when login is eventually handled
    //Throwing generic error here as Angular would automatically turn a backend 401 response status into an error - simulating the expected Angular result in receiving 401 Unauthorized
    authServiceSpyObj.login.and.returnValue(throwError(() => ({ Error })));
    //Set value of email and password to fulfil login requirements - simulate values entered into form fields
    component.loginForm.setValue({ email: 'wrong@email.com', password: 'qwerty456' });

    //Act
    //Fire login request - will throw error due to above HTTP response
    component.handleLogin();

    //Assert
    //Login should be called on mock AuthService
    expect(authServiceSpyObj.login).toHaveBeenCalled();
    //Error should contain correrct error message as per login.ts instructions
    expect(component.loginError()).toContain('Invalid email or password');
    //No navigation should take place on unsuccessful logins
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('shows login form field error when no credentials are entered', () => {
    //Arrange
    //Set value of email and password to empty string values - simulate no data entered into form fields
    component.loginForm.setValue({ email: '', password: '' });
    //Manually set the FormGroup state to has been touched to ensure error messages appear in fixture
    component.loginForm.get('email')?.markAsTouched();
    component.loginForm.get('password')?.markAsTouched();
    //Act
    //Fire login request - will throw error due to above HTTP response
    component.handleLogin();
    fixture.detectChanges();

    const element = fixture.nativeElement;
    const emailError = element.querySelector('[text-id="email-error"]')?.textContent;
    const passwordError = element.querySelector('[text-id="password-error"]')?.textContent;

    //Assert
    //Login should be called on mock AuthService
    expect(authServiceSpyObj.login).not.toHaveBeenCalled();
    //Error should contain correrct error message as per login.ts instructions
    expect(emailError).toContain('Email address is required.');
    expect(passwordError).toContain('Password is required.');
    //No navigation should take place on unsuccessful logins
    expect(router.navigate).not.toHaveBeenCalled();
  });
});

//Terminal commands to run this specific test - first bypasses security policy that prevents execution of scripts from terminal just for this session
//Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
//ng test --include=src/app/features/login/login.spec.ts
