import 'zone.js/testing';
import { TestBed } from '@angular/core/testing';
import {
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting,
} from '@angular/platform-browser-dynamic/testing';

// Initialize the Angular testing environment.
// This is STILL the standard way, but the "require.context" 
// part below it is what is actually dead.
TestBed.initTestEnvironment(
  BrowserDynamicTestingModule,
  platformBrowserDynamicTesting()
);

