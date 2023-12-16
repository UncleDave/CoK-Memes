import { RouterProvider } from "react-router-dom";
import GlobalStyles from "./features/core/GlobalStyles.tsx";
import Reset from "./features/core/Reset.tsx";
import router from "./features/core/router.tsx";

const App = () => {
  return (
    <>
      <Reset />
      <GlobalStyles />
      <RouterProvider router={router} />
    </>
  );
};

export default App;
