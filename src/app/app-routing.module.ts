import { NgModule } from '@angular/core';
import { PreloadAllModules, RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'home', loadChildren: () => import('./home/home.module').then( m => m.HomePageModule)},
  { path: 'auth', loadChildren: './auth/auth.module#AuthPageModule' },
  { path: 'scenery', loadChildren: './scenery/scenery.module#SceneryPageModule' },
  { path: 'observe', loadChildren: './scenery/observe/observe.module#ObservePageModule' },
  { path: 'my-scenes', loadChildren: './scenery/my-scenes/my-scenes.module#MyScenesPageModule' },
  { path: 'new-scene', loadChildren: './scenery/my-scenes/new-scene/new-scene.module#NewScenePageModule' },
  { path: 'edit-scene', loadChildren: './scenery/my-scenes/edit-scene/edit-scene.module#EditScenePageModule' },
  { path: 'scene-detail', loadChildren: './scenery/observe/scene-detail/scene-detail.module#SceneDetailPageModule' },
  { path: 'share-scenery', loadChildren: './scenery/my-scenes/share-scenery/share-scenery.module#ShareSceneryPageModule' },
  { path: 'visits', loadChildren: './visits/visits.module#VisitsPageModule' },
];

@NgModule({
  imports: [
    RouterModule.forRoot(routes, { preloadingStrategy: PreloadAllModules })
  ],
  exports: [RouterModule]
})
export class AppRoutingModule { }
