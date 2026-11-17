import { Routes } from '@angular/router';

export const routes: Routes = [
	{
		path: '',
		loadComponent: () => import('./pages/login.component').then((module) => module.LoginComponent)
	},
	{
		path: 'simulated-idp-login',
		loadComponent: () => import('./pages/simulated-idp-login.component').then((module) => module.SimulatedIdpLoginComponent)
	},
	{
		path: 'auth-callback',
		loadComponent: () => import('./pages/auth-callback.component').then((module) => module.AuthCallbackComponent)
	},
	{
		path: 'dashboard',
		loadComponent: () => import('./pages/dashboard.component').then((module) => module.DashboardComponent)
	},
	{ path: '**', redirectTo: '' }
];
