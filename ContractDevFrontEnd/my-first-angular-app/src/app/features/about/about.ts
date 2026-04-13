import { Component, ElementRef, ViewChild, ViewEncapsulation, signal, Input } from '@angular/core';
import { AboutText } from "../about-text/about-text";
import { MatTooltipModule } from '@angular/material/tooltip';
import { Modal } from 'bootstrap';
import { GdprModal } from '../../core/shared/components/gdpr-modal/gdpr-modal';



@Component({
  selector: 'app-about',
  imports: [AboutText, MatTooltipModule, GdprModal],
  templateUrl: './about.html',
  styleUrl: './about.css',
  encapsulation: ViewEncapsulation.None //this allows global styles in
})

export class About{
  // Reference to the section we want to scroll to
  @ViewChild('aboutSection') aboutSection!: ElementRef;
  

  // Signals for UI state
  showVideo = signal(false);
  showAboutText = signal(false);
  showGdpr = signal(false);

  // Show video on click
  showVideoOn(){
    this.showVideo.set(true);
  }

  // Reset video when it ends
  onVideoEnding(){
    this.showVideo.set(false);
  }

  // Toggle About section and scroll into view when opening
  toggleAboutText() {
    // Flip the boolean state
    this.showAboutText.update(v => !v);

    //scroll only when opening, not closing
    if(!this.showAboutText()) return;
    

    // Wait for Angular to render the DOM before scrolling
    setTimeout(() => {
      this.aboutSection?.nativeElement?.scrollIntoView({
        behavior: 'smooth',
        block: 'start'
      });
    }, 50);
  }

  //Display the GDPR modal
  onShowGdpr() {
    const modalEl = document.getElementById('gdprModal') as any;
    //hide buttons when opened from About page
    modalEl.showButtons = false;
    const modal = new Modal(modalEl!);
    modal.show();
  }
}
