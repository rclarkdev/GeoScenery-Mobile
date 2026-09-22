import { enableProdMode } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { addIcons } from 'ionicons';
import { add, cameraOutline, globe, locateOutline, personCircleOutline, search } from 'ionicons/icons';

import { AppModule } from './app/app.module';
import { environment } from './environments/environment';

if (environment.production) {
  enableProdMode();
}

// Register icons locally so they render on native devices without a network fetch
addIcons({
  add,
  'camera-outline': cameraOutline,
  globe,
  'locate-outline': locateOutline,
  'person-circle-outline': personCircleOutline,
  search
});

platformBrowserDynamic().bootstrapModule(AppModule)
  .catch(err => console.log(err));
