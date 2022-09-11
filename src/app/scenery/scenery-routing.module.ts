import { RouterModule, Routes } from '@angular/router';
import { SceneryPage } from './scenery.page';
import { NgModule } from '@angular/core';

const routes: Routes = [
    {
        path: 'tabs',
        component: SceneryPage,
        children: [
            {
                path: 'observe',
                children: [
                    {
                        path: '',
                        loadChildren: () => import('./observe/observe.module').then(m => m.ObservePageModule)
                    },
                    {
                        path: ':sceneId',
                        loadChildren: () => import('./observe/scene-detail/scene-detail.module').then(m => m.SceneDetailPageModule)
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
                path: '',
                redirectTo: '/scenery/tabs/observe',
                pathMatch: 'full'
            }
        ]
    },
    {
        path: '',
        redirectTo: '/scenery/tabs/observe',
        pathMatch: 'full'
    }
];

@NgModule({
    imports: [RouterModule.forChild(routes)],
    exports: [RouterModule]
})

export class SceneryRoutingModule { }