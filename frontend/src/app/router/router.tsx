import { createBrowserRouter } from 'react-router-dom'
import { HomePage } from '@/pages/home'
import { DashboardPage } from '@/pages/dashboard'
import { LoginPage } from '@/pages/login'
import { AdminPage } from '@/pages/admin'
import { CategoriesPage } from '@/pages/categories'
import { BudgetsPage } from '@/pages/budgets'
import { AccrualsPage } from '@/pages/accruals'
import { AccrualDetailPage } from '@/pages/accrual-detail'
import { ScanPage } from '@/pages/scan'
import { ComingSoonPage } from '@/pages/coming-soon'
import { AppShell } from '@/widgets/app-shell'
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
      {
        element: <AppShell />,
        children: [
          { path: '/', element: <HomePage /> },
          { path: '/dashboard', element: <DashboardPage /> },
          { path: '/accruals', element: <AccrualsPage /> },
          { path: '/accruals/:id', element: <AccrualDetailPage /> },
          { path: '/categories', element: <CategoriesPage /> },
          { path: '/budgets', element: <BudgetsPage /> },
          { path: '/journal', element: <ComingSoonPage title="Журнал изменений" /> },
          { path: '/scan', element: <ScanPage /> },
          {
            element: <AdminRoute />,
            children: [{ path: '/admin', element: <AdminPage /> }],
          },
        ],
      },
    ],
  },
])
