/*
 * @author Jeán Walton
 * 14/04/2026
 * password-recovery.spec.ts
 */

import { ComponentFixture, fakeAsync, TestBed } from '@angular/core/testing';
import { PasswordRecovery } from './password-recovery';
import { UserService } from '../../core/services/user.service';
import { Router } from '@angular/router';
import { of, throwError, Subject } from 'rxjs';
import { tick } from '@angular/core/testing';

describe('PasswordRecovery', () => {
  // this is the component being tested
  let component: PasswordRecovery;
  //the wrapper that handles the DOM 
  let fixture: ComponentFixture<PasswordRecovery>;
  //jasmine spy version of UserService (fake service API Calls)
  let userServiceSpy: jasmine.SpyObj<UserService> 
  //testing the that the router can be called
  let routerSpy: jasmine.SpyObj<Router>
  

  beforeEach(async () => {
     //create fake UserService with only the method needed
    userServiceSpy = jasmine.createSpyObj('UserService', ['accountRecovery']);

    //creat a fake router service
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    await TestBed.configureTestingModule({
      imports: [PasswordRecovery],
      providers: [
        //This tells the Rest of test to use the spies
        {provide: UserService, useValue: userServiceSpy},
        {provide: Router, useValue: routerSpy}
      ]
    })
    //THIS TO KILL THE REAL SERVICE CALL: override the component itself
    .overrideComponent(PasswordRecovery, {
      set: { providers: [{ provide: UserService, useValue: userServiceSpy }] 
    }
    })
    .compileComponents();

    //creating a instance of the component
    fixture = TestBed.createComponent(PasswordRecovery);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });



  //fuction to fill out form with valid data 
  function fillFormData() {
    component.passwordResetForm.setValue({
      email: 'test@example.com',
      securityQuestion: 'What was the name of your first pet?',
      securityAnswer: 'Flash',
      newPassword: 'applesauce123',
      confirmNewPassword: 'applesauce123'
    });
  }


  it('should create', () => {
    expect(component).toBeTruthy();
  });


  //tests
  it('should not call accountRecovery when form is invalid', () => {
    //call the method with a empty form
    component.handleAccountRecovery();

    //expected service (not) to be called
    expect(userServiceSpy.accountRecovery).not.toHaveBeenCalled();

    //expected isloading signal state to remain false
    expect(component.isLoading()).toBeFalse();
  });

  it('should call accountRecovery with a valid form (FormData)', () =>{
    //use the fill form function a bove
    fillFormData();

    //simulate a successful API call (Its fake,  (of) creates an observable)
    userServiceSpy.accountRecovery.and.returnValue(of('Success'));

    //act calling method
    component.handleAccountRecovery();

    //ensure service was called only once
    expect(userServiceSpy.accountRecovery).toHaveBeenCalledTimes(1);

    //the userServiceSpy that was called earlier, get the last call checks the first value passed and treats it as FormData
    const formDataArg = userServiceSpy.accountRecovery.calls.mostRecent().args[0] as FormData;

    //check if it's actually FormData
    expect(formDataArg instanceof FormData).toBeTrue();

    
    //verify that each field was added correctly
    expect(formDataArg.get('Email')).toBe('test@example.com');
    expect(formDataArg.get('SecurityQuestion')).toBe('What was the name of your first pet?');
    expect(formDataArg.get('SecurityAnswer')).toBe('Flash');
    expect(formDataArg.get('NewPassword')).toBe('applesauce123');
    expect(formDataArg.get('ConfirmNewPassword')).toBe('applesauce123');
  });

  it('should set state to success and navigate to login page after successful recovery', fakeAsync(() => {
    //use the fill form function a bove
    fillFormData();

    //simulate a successful API call (Its fake, (of) creates an observable)
    userServiceSpy.accountRecovery.and.returnValue(of('Success'));

    //act calling method
    component.handleAccountRecovery();

    //ensure service was called only once
    expect(userServiceSpy.accountRecovery).toHaveBeenCalledTimes(1);

    // after a success
    expect(component.isLoading()).toBeFalse();
    expect(component.loginError()).toBe('');
    expect(component.updatedPassword()).toBeTrue();

    //simulate the passing of time 2sec
    tick(2000);

    expect(component.updatedPassword()).toBeFalse(); //cleared
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']); //route to login page
  }));

  it('form should show error and reset after failed recovery', fakeAsync( () => {
    //use the fill form function above
    fillFormData();

    //simulate a unsuccessful API call (Its fake, throwing an error)
    userServiceSpy.accountRecovery.and.returnValue(throwError(() => new Error('Recovery Failed')));

    //act calling method
    component.handleAccountRecovery();

    expect(component.isLoading()).toBeFalse();
    expect(component.loginError()).toBe('Recovery Failed');

    //simulate the passing of time 2sec
    tick(2500);

    expect(component.loginError()).toBe(''); //cleared
    
    //passwordResetForm should be reset
    expect(component.passwordResetForm.value).toEqual({
      email: null,
      securityQuestion: null,
      securityAnswer: null,
      newPassword: null,
      confirmNewPassword: null
    })
  }));

  it('should set loading to true while waiting for the password reset API response', () => {
    //use the fill form function above
    fillFormData();

    //create a controllable observable (manual response)
    const manualResponse = new Subject<string>();

    //return this 'pending' observable
    userServiceSpy.accountRecovery.and.returnValue(manualResponse.asObservable());

    //act
    component.handleAccountRecovery();

    // after call the loading state should be true
    expect(component.isLoading()).toBeTrue();

    //simulating the manualResponse arriving
    manualResponse.next('fake success message');
    manualResponse.complete();

    // after call the loading state should be false
    expect(component.isLoading()).toBeFalse();
  });
});
