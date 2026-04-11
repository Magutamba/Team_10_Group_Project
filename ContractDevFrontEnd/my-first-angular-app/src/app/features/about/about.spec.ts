//This is the modern Vitest way of doing things
import { render } from '@testing-library/angular';
import { About } from './about';

describe('About', () => {
  it('should create', async () => {
    const { fixture } = await render(About);
    expect(fixture.componentInstance).toBeTruthy();
  });
});







/*this is the generated test given automatically (jasmine, karma) out dated
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { About } from './about';

describe('About', () => {
  let component: About;
  let fixture: ComponentFixture<About>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [About]
    })
    .compileComponents();

    fixture = TestBed.createComponent(About);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});*/
