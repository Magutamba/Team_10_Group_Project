// Add fakeAsync and tick here
import { ComponentFixture, TestBed, fakeAsync, tick, flush } from '@angular/core/testing';
import { About } from './about';

describe('About', () => {
  let component: About;
  let fixture: ComponentFixture<About>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [About]
    })
    .compileComponents();

    fixture = TestBed.createComponent(About); //creates the component in the lab
    component = fixture.componentInstance; // gives access to the TS class
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

 // TEST 1: Testing Signals
  it('should start with showVideo as false', () => {
    // To read a signal in a test, you call it like a function: signalName()
    expect(component.showVideo()).toBeFalse();
  });

  it('should set showVideo to true when showVideoOn is called', () => {
    component.showVideoOn();
    expect(component.showVideo()).toBeTrue();
  });

  // TEST 2: Testing Toggles
  it('should toggle showAboutText signal', () => {
    expect(component.showAboutText()).toBeFalse();
    component.toggleAboutText();
    expect(component.showAboutText()).toBeTrue();
    component.toggleAboutText();
    expect(component.showAboutText()).toBeFalse();
  });


  // TEST 3: Testing Async Scrolling
  // We use fakeAsync to 'pause' time and control the setTimeout
  it('should attempt to scroll when opening text', fakeAsync(() => {
    
    // 1. THE SPY (The Wiretap)
    // We spy on the 'Prototype' of HTMLElement. 
    // This means if ANY HTML element on the page calls scrollIntoView, we will know.
    const scrollSpy = spyOn(HTMLElement.prototype, 'scrollIntoView');

    // 2. THE STATE (The Setup)
    // We force the signal to false. 
    // This ensures that when we call 'toggle', it becomes TRUE (Opening).
    component.showAboutText.set(false);
    
    // We tell Angular to process that signal change immediately.
    fixture.detectChanges(); 

    // 3. THE MOCK (The Fake Element)
    // Even though we are spying on the Prototype, the component still needs
    // an object to call the function on. We manually 'inject' a dummy div.
    (component as any).aboutSection = { 
      nativeElement: document.createElement('div') 
    };

    // 4. THE ACT (The Trigger)
    // This flips the signal to 'true' and starts the 50ms setTimeout.
    component.toggleAboutText();
    
    // 5. THE TIME TRAVEL (The Fast-Forward)
    // tick(50) moves the clock forward to trigger the setTimeout.
    // flush() clears out any other tiny micro-tasks to ensure a clean finish.
    tick(50);
    flush(); 
    
    // 6. THE ASSERT (The Proof)
    // If the logic worked, our 'wiretap' on the Prototype should have recorded 1 hit.
    expect(scrollSpy).toHaveBeenCalled();
  }));
});
