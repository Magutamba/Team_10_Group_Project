// NOT USED FOR FUTURE USE //
import { render } from '@testing-library/angular';
import { BillingDetails } from './billing-details';

describe('AboutText', () => {
  it('should create', async () => {
    const { fixture } = await render(BillingDetails);
    expect(fixture.componentInstance).toBeTruthy();
  });
});





/*import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BillingDetails } from './billing-details';

describe('BillingDetails', () => {
  let component: BillingDetails;
  let fixture: ComponentFixture<BillingDetails>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BillingDetails]
    })
    .compileComponents();

    fixture = TestBed.createComponent(BillingDetails);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});*/
