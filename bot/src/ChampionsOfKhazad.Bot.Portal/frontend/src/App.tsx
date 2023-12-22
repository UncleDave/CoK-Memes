import { RouterProvider } from "react-router-dom";
import router from "./features/core/router.tsx";
import { CssBaseline, CssVarsProvider } from "@mui/joy";

const App = () => {
  return (
    <CssVarsProvider defaultMode="system">
      <CssBaseline />
      <RouterProvider router={router} />
    </CssVarsProvider>
  );
};

export default App;
