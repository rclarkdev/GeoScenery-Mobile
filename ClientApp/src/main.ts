import { enableProdMode } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { defineCustomElements } from '@ionic/pwa-elements/loader';
import { addIcons } from 'ionicons';
import { add, cameraOutline, close, createOutline, globe, locateOutline, optionsOutline, personAddOutline, personCircleOutline, personRemoveOutline, search } from 'ionicons/icons';

import { AppModule } from './app/app.module';
import { environment } from './environments/environment';

if (environment.production) {
  enableProdMode();
}

defineCustomElements(window);

// Register icons locally so they render on native devices without a network fetch
addIcons({
  add,
  'camera-outline': cameraOutline,
  close,
  'create-outline': createOutline,
  globe,
  'locate-outline': locateOutline,
  'options-outline': optionsOutline,
  'person-add-outline': personAddOutline,
  'person-circle-outline': personCircleOutline,
  'person-remove-outline': personRemoveOutline,
  search
});

platformBrowserDynamic().bootstrapModule(AppModule)
  .catch(err => console.log(err));
