import { RouterModule, Routes } from '@angular/router';
import { SceneryPage } from './scenery.page';
import { NgModule } from '@angular/core';

const routes: Routes = [
    {
        path: 'tabs',
        component: SceneryPage,
        children: [
            {
                path: 'search',
                children: [
                    {
                        path: '',
                        loadChildren: () => import('./search/search.module').then(m => m.SearchPageModule)
                    },
                    {
                        path: ':sceneId',
                        loadChildren: () => import('./search/scene-detail/scene-detail.module').then(m => m.SceneDetailPageModule)
                    }
                ]
            },
            {
                path: 'my-scenes',
                children: [
                    {
                        path: '',
                        loadChildren: () => import('./my-scenes/my-scenes.module').then(m => m.MyScenesPageModule)
                    },
                     {
                        path: 'new',
                        loadChildren: () => import('./my-scenes/new-scene/new-scene.module').then(m => m.NewScenePageModule)
                    },
                    {
                        path: 'edit/:sceneId',
                        loadChildren: () => import('./my-scenes/edit-scene/edit-scene.module').then(m => m.EditScenePageModule)
                    },
                    {
                        path: ':sceneId',
                        loadChildren: () => import('./my-scenes/share-scenery/share-scenery.module').then(m => m.ShareSceneryPageModule)
                    }
                ]
            },
            {
                path: 'profile',
                loadChildren: () => import('./profile/profile.module').then(m => m.ProfilePageModule)
            },
            {
                path: '',
                redirectTo: '/scenery/tabs/search',
                pathMatch: 'full'
            }
        ]
    },
    {
        path: '',
        redirectTo: '/scenery/tabs/search',
        pathMatch: 'full'
    }
];

@NgModule({
    imports: [RouterModule.forChild(routes)],
    exports: [RouterModule]
})

export class SceneryRoutingModule { }