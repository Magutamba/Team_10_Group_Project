/*
 * @author Jeán Walton
 * 13/04/2026
 * home.spec.ts
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Home } from './home';


//describe defines a Test. It's like a container
describe('Home', () => {
  //instatance of the home.ts class
  let component: Home;
  //wrapper around the html template and component
  let fixture: ComponentFixture<Home>;


  //beforeEach runs before every single test block as it's a  asynchronous process.
  beforeEach(async () => {
    
    //TestBed is creates a fake Angular module enviroment for the test.
    await TestBed.configureTestingModule({
      //imports componts 
      imports: [Home]
    })
    .compileComponents();

    //createComponent it creates a class and the HTML rendering
    fixture = TestBed.createComponent(Home);

    //grabs the actual instant from the of the home class 
    component = fixture.componentInstance;

    //awaits for asynchronous (like the video loading) to finish
    await fixture.whenStable();
  });

  //test case
  it('should create', () => {
    //expected outcome to be true no (component is created)
    expect(component).toBeTruthy();
  });

  it('should change the quote index when nextQuote is called', () => {
    //check that signal starts at 0
    expect(component.index()).toBe(0);

    //call the function
    component.nextQuote();

    //check if value updated
    expect(component.index()).toBe(1);
  });
  
  it('should display an actual quote in the html', () => {
    //check the for changes
    fixture.detectChanges();

    //HTML 'Body' element that hold the quote
    const element = fixture.nativeElement;
    //inside the p tag
    const quoteText = element.querySelector('p')?.textContent;

    //checks is the HTML contains the first quote from the query
    expect(quoteText).toContain(component.quotes[0]);
  }); 

  it('should loop back to the first quote after the last one', () => {
    //setting the signal to the last one in the array
    const lastIndex = component.quotes.length -1;
    //set component index to the last
    component.index.set(lastIndex);

    //call the function
    component.nextQuote();

    //this should 0 
    expect(component.index()).toBe(0);

  });
});
