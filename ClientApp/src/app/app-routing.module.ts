import { NgModule } from '@angular/core';
import { PreloadAllModules, RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './auth/auth.guard';

const routes: Routes = [
  { path: '', redirectTo: 'scenery', pathMatch: 'full' },
  { path: 'auth', loadChildren: () => import('./auth/auth.module').then(m => m.AuthPageModule) },
  { path: 'scenery', canActivate: [AuthGuard], loadChildren: () => import('./scenery/scenery.module').then(m => m.SceneryPageModule) },
  { path: 'visits', canActivate: [AuthGuard], loadChildren: () => import('./visits/visits.module').then(m => m.VisitsPageModule) }
];

@NgModule({
  imports: [
    RouterModule.forRoot(routes, { preloadingStrategy: PreloadAllModules })
  ],
  exports: [RouterModule]
})
export class AppRoutingModule { }
