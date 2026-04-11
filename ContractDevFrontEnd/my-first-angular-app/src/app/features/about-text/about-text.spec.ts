import { render } from '@testing-library/angular';
import { AboutText } from './about-text';

describe('AboutText', () => {
  it('should create', async () => {
    const { fixture } = await render(AboutText);
    expect(fixture.componentInstance).toBeTruthy();
  });
});







/*import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AboutText } from './about-text';

describe('AboutText', () => {
  let component: AboutText;
  let fixture: ComponentFixture<AboutText>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AboutText]
    })
    .compileComponents();
    
    // Create an instance of the component and its test fixture
    fixture = TestBed.createComponent(AboutText);
    component = fixture.componentInstance;
    //Wait for any async lifecycle hooks to complete
    await fixture.whenStable();
  });
  //Basic sanity test: verifies the component is created successfully
  it('should create', () => {
    expect(component).toBeTruthy();
  });
});*/
