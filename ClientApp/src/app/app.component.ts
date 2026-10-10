import { Component } from '@angular/core';

import { Platform } from '@ionic/angular';
import { SiteIdentityService } from './platform/site-identity.service';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  styleUrls: ['app.component.scss']
})
export class AppComponent {
  constructor(
    private platform: Platform,
    private siteIdentity: SiteIdentityService
  ) {
    void this.siteIdentity.initialize();
    this.initializeApp();
  }

  initializeApp() {
    this.platform.ready().then(() => {
      // Native shell initialization is handled by Capacitor plugins.
    });
  }
}
