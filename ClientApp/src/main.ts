import { enableProdMode } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { addIcons } from 'ionicons';
import { add, cameraOutline, globe, locateOutline, personAddOutline, personCircleOutline, personRemoveOutline, search } from 'ionicons/icons';

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
  'person-add-outline': personAddOutline,
  'person-circle-outline': personCircleOutline,
  'person-remove-outline': personRemoveOutline,
  search
});

platformBrowserDynamic().bootstrapModule(AppModule)
  .catch(err => console.log(err));
