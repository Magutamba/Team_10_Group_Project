/*
 * @author Jeán Walton
 * 14/04/2026
 * user-profile-deletion.spec.ts
 */

import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { UserProfileDeletion } from './user-profile-deletion';
import { ReactiveFormsModule } from '@angular/forms';
import { of, Subject, throwError } from 'rxjs';
import { UserService } from '../../core/services/user.service';
import { AuthService } from '../../core/auth/auth.service';
import { Router } from '@angular/router';
import { inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';



describe('UserProfileDeletion', () => {
  let component: UserProfileDeletion;
  let fixture: ComponentFixture<UserProfileDeletion>;
  let userServiceSpy!: jasmine.SpyObj<UserService>;
  let authServiceSpy!: jasmine.SpyObj<AuthService>;

  let fakeRouter!: Router;
  
  beforeEach(async () => {
    userServiceSpy = jasmine.createSpyObj('UserService', ['deleteUserAccount']);
    authServiceSpy = jasmine.createSpyObj('AuthService', ['getCurrentUserId', 'logout']);

    await TestBed.configureTestingModule({
      // needed so the reactive form works
      imports: [ReactiveFormsModule],

      // here we replace real services with our fake ones
      // this lets us control what they return in tests
      providers: [
        { provide: UserService, useValue: userServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
        //router testing support
        provideRouter([]),
        //RouterLink needs ActivatedRoute
        { provide: ActivatedRoute, useValue: {}}
      ]
    }).compileComponents();

    // instance of the component
    fixture = TestBed.createComponent(UserProfileDeletion);
    component = fixture.componentInstance;

    fakeRouter = TestBed.inject(Router);

    // default fake user id
    authServiceSpy.getCurrentUserId.and.returnValue(1);

    // run Angular change detection
    fixture.detectChanges();
  });

  //form fill function (helper)
  function fillFormData() {
    //access to the formGroup: deletionform
    component.deletionForm.setValue({
      currentPassword: 'applesauce',
      securityQuestion: 'What city were you born in?',
      securityAnswer: 'Dublin'
    });
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  //tests
  it("should NOT call deleteUserAccount (API) when form is invalid", () => {
    //create a invalid form
    component.deletionForm.setValue({
      currentPassword: '',
      securityQuestion: '',
      securityAnswer: ''
    });

    //act 
    component.deleteProfile();
    
    //what to expect
    expect(userServiceSpy.deleteUserAccount).not.toHaveBeenCalled();
  });

  it('should send correct FormData when form is valid', () =>{
    //call the helper form function
    fillFormData();

    //configure spy 
    userServiceSpy.deleteUserAccount.and.returnValue(of());

    //act 
    component.deleteProfile();

    //expected the API was called
    expect(userServiceSpy.deleteUserAccount).toHaveBeenCalled(); 

    //lets check what was pass into the api
    const formDataArg = userServiceSpy.deleteUserAccount.calls.mostRecent().args[0] as FormData;
    
    expect(formDataArg instanceof FormData).toBeTrue();
    expect(formDataArg.get('Id')).toBe('1');
    expect(formDataArg.get('Password')).toBe('applesauce');
    expect(formDataArg.get('SecurityAnswer')).toBe('Dublin');
  
  });

  it('should show success on deletion, clear and navigate to home page', fakeAsync( () =>{
    //call the form helper function
    fillFormData();

    //configure the fake successfull API response
    userServiceSpy.deleteUserAccount.and.returnValue(of(void 0));


    //spy on router navigation
    spyOn(fakeRouter, 'navigate');

    //act
    component.deleteProfile();

    expect(component.deleteSuccess()).toBeTrue();
    expect(component.deleteError()).toBe('');
    expect(userServiceSpy.deleteUserAccount).toHaveBeenCalledTimes(1);

    //simulate passing time
    tick(2000);
  

    //after time out success UI should clear and redirect
    expect(component.deleteSuccess()).toBeFalse();
    expect(fakeRouter.navigate).toHaveBeenCalledOnceWith(['/home']);
  }));
  
  it('should show error, clear UI and navigate back on failure', fakeAsync(() => {
    //call the form helper function
    fillFormData();

    //simulate a failed API call
    userServiceSpy.deleteUserAccount.and.returnValue(throwError(() => new Error('Delete failed')));

    //spy on the router navigation 
    spyOn(fakeRouter, 'navigate');

    //act
    component.deleteProfile();

    //expecting state set to false and a error message to appear
    expect(component.deleteSuccess()).toBeFalse();
    expect(component.deleteError()).toBe('Failed to Delete User Account: Delete failed');

    //simulate passing time
    tick(2000);

    //expected to be cleared after time had passed redirected back to the page
    expect(component.deleteError()).toBe('');
    expect(fakeRouter.navigate).toHaveBeenCalledWith(['/userProfileDeletion']);
  }));

  it('should open the delete confirm modal', () => {
    //calls the openModal method 
    component.openModal();
    //expected the modal should be open
    expect(component.isOpenDeleteModal()).toBeTrue();
  });

  it('should close the delete confirm modal', () => {
    //set the modal to be open
    component.isOpenDeleteModal.set(true);
    //call the closeModal method 
    component.closeModal();
    //expect the modal to be closed
    expect(component.isOpenDeleteModal()).toBeFalse();
  });

  it('should call deleteProfile, logout, and close modal on confirmDelete', () => {
    //checking on these methods if (to see if they are called) 
    spyOn(component, 'deleteProfile');
    spyOn(component, 'closeModal');
    //call on the confirmDelete method
    component.confirmDelete();
    //now we check have these methods been called.
    expect(component.deleteProfile).toHaveBeenCalled();
    expect(authServiceSpy.logout).toHaveBeenCalled();
    expect(component.closeModal).toHaveBeenCalled();
  });
})
