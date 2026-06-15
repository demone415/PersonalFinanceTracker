import { createBrowserRouter } from 'react-router-dom'
import { HomePage } from '@/pages/home'
import { LoginPage } from '@/pages/login'
import { AdminPage } from '@/pages/admin'
import { CategoriesPage } from '@/pages/categories'
import { AccrualsPage } from '@/pages/accruals'
import { AccrualDetailPage } from '@/pages/accrual-detail'
import { ProtectedRoute } from './ProtectedRoute'
import { AdminRoute } from './AdminRoute'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: <ProtectedRoute />,
    children: [
      { path: '/', element: <HomePage /> },
      { path: '/categories', element: <CategoriesPage /> },
      { path: '/accruals', element: <AccrualsPage /> },
      { path: '/accruals/:id', element: <AccrualDetailPage /> },
    ],
  },
  {
    element: <AdminRoute />,
    children: [{ path: '/admin', element: <AdminPage /> }],
  },
])
